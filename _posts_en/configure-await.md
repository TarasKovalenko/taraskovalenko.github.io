---
title: A complete guide to ConfigureAwait in .NET
author: Taras Kovalenko
date: 2025-03-03 09:00:00.000000000 +02:00
categories:
- ".net"
- C#
- performance
- asynchronous programming
tags:
- ".net"
- C#
- async
- await
- synchronizationContext
- task
- threading
mermaid: true
lang: en
locale: en_US
translation_key: configure-await
permalink: "/en/posts/configure-await/"
---

Asynchronous programming has become the basis of modern development on the .NET platform.
The `async/await` mechanism greatly simplified the work with asynchronous operations, but brought with it certain nuances that are important to understand in order to create effective applications. One of the key aspects of asynchronous programming in .NET is the `ConfigureAwait` method, which allows you to control where the execution of the asynchronous method continues after `await`.

## Synchronization context and await behavior

### What is a synchronization context?

The synchronization context (`SynchronizationContext`) is an abstraction that controls where code will execute after returning from an asynchronous operation. This is a kind of "router" that determines on which thread to continue the execution of the code.
Different types of applications use different implementations of the synchronization context:

- `Windows Forms`, `WPF`, `MAUI` have a UI thread synchronization context that ensures that code after `await` is executed on the UI thread, allowing UI elements to be updated safely
- Classic `ASP.NET` has its own synchronization context associated with the request that stores the `HttpContext` context
- `ASP.NET Core`, console applications and services usually do not have a dedicated synchronization context and use threads from the thread pool

When you use `await` in an asynchronous method, .NET defaults to:

- Grabs the current sync context
- Performs the expected asynchronous operation
- Returns code execution after await to the captured context

This is convenient for developers because it allows you to naturally work with `UI` components after asynchronous operations. However, this behavior comes at a price in terms of performance.

### Problems with the synchronization context

Returning to a captured context can create several problems:

- Additional overhead — switching between contexts requires system resources
- Potential deadlocks - deadlocks can occur in some scenarios (especially with blocking code)
- Unnecessary overhead - in many cases the code does not need the original context to continue working

It was to solve these problems that the `ConfigureAwait` method was created.

## ConfigureAwait(bool continueOnCapturedContext)

The `ConfigureAwait` method allows you to override the default behavior of `await` to return to the captured context.
It has one boolean parameter that specifies whether to return to the original context after `await`:

```cs
// Grabs the context and returns to it after await (default behavior)
await someTask.ConfigureAwait(true);
// or simply
await someTask;

// Does NOT return to captured context, continues on any available thread
await someTask.ConfigureAwait(false);
```

### How ConfigureAwait(false) works

When you use ConfigureAwait(false):

- The context is still captured when calling `await`
- An asynchronous operation is performed in the same way as always
- After the operation completes, instead of returning to the captured context, continuation is performed on any available thread from the thread pool
- This allows you to avoid the costs of context switching and potential deadlocks

```mermaid
sequenceDiagram
    participant UI as UI thread
    participant TP as A thread pool thread
    participant IO as I/O (Network/Disk)
    
    Note over UI: Pressing the button
    
    %% ConfigureAwait(true)
    rect rgb(100, 156, 239)
        Note over UI, IO: ConfigureAwait(true) or no ConfigureAwait
        UI->>UI: Async method call
        UI->>UI: Capturing the UI context
        UI->>IO: Start of asynchronous operation
        Note over UI: The thread is freed for the UI
        IO-->>TP: Completion of the operation
        TP-->>UI: Return to the UI thread
        UI->>UI: Interface update
    end
    
    %% ConfigureAwait(false)
    rect rgb(205, 165, 222)
        Note over UI, IO: ConfigureAwait(false)
        UI->>UI: Async method call
        UI->>UI: Capturing the UI context (but not using it)
        UI->>IO: Start of asynchronous operation
        Note over UI: The thread is freed for the UI
        IO-->>TP: Completion of the operation
        Note over TP: Continuation in pool thread
        TP->>TP: Attempting to update UI (error)
    end
```

### Execution flow with ConfigureAwait

```mermaid
flowchart TB
    Start([The beginning of the method]) --> CaptureContext[Capturing the current context]
    CaptureContext --> AsyncOperation[Starting an asynchronous operation]
    AsyncOperation --> OpComplete{Is the operation complete?}
    OpComplete -->|Yes| ConfigAwait{ConfigureAwait false?}
    OpComplete -->|No| WaitForComplete[Release thread Waiting for completion]
    WaitForComplete --> Complete[The operation is complete]
    Complete --> ConfigAwait
    
    ConfigAwait -->|Yes| ThreadPool[Using a thread pool]
    ConfigAwait -->|No| OrigContext[Return to the original context]
    
    ThreadPool --> NextCode[Executing the following code]
    OrigContext --> NextCode
    
    NextCode --> End([End of method])
```

## When to use ConfigureAwait(false)

ConfigureAwait(false) works best for the following scenarios:

### In the library code

Library code is often used in many different types of applications. You don't know in advance whether your library will be used in `WPF, ASP.NET`, a console application, or a mobile application. By using `ConfigureAwait(false)`, you ensure that your library does not impose unnecessary restrictions on the application that uses it.
Developing libraries requires special attention to context, as different applications may expect different behavior. If you are developing a general purpose library, the best approach is to use `ConfigureAwait(false)` fully and consistently for all await in your code to make it most flexible and efficient.

### For operations that do not interact with the context

Many asynchronous operations do not need the original context to continue execution. For example, reading from a file, processing data, HTTP requests - all this can be done in any thread. Using `ConfigureAwait(false)` in such scenarios improves performance without affecting functionality.

```cs
public async Task<ProcessedData> ProcessDataAsync(string filePath)
{
    // Reading a file does not require a special context
    string content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
    
    // Data analysis can also be performed in any thread
    var parsedData = await JsonSerializer.DeserializeAsync<RawData>(
        new MemoryStream(Encoding.UTF8.GetBytes(content))).ConfigureAwait(false);
    
    // Data processing is context-independent
    return await TransformDataAsync(parsedData).ConfigureAwait(false);
}
```

### To increase productivity

Even if your application is not at risk of deadlocks, `ConfigureAwait(false)` can improve performance, especially in high-load scenarios where a large number of asynchronous operations are performed simultaneously.
In large web applications that handle thousands of requests, eliminating unnecessary context switches can significantly improve system throughput. Each context switch has a small overhead, but at scale it can have a significant impact on overall performance.

### To prevent deadlocks in specific scenarios

In some architectural patterns, deadlocks may occur when mixing synchronous and asynchronous operations. `ConfigureAwait(false)` can help avoid such deadlocks, although it is not a recommended approach to solve the problem.

A typical deadlock scenario occurs when:

- A synchronous method blocks a thread while waiting for the result of an asynchronous method
- An asynchronous method tries to return to a captured context that is already locked

`ConfigureAwait(false)` breaks this connection, allowing the asynchronous method to continue execution on any thread rather than waiting for the locked context to be released.

## When NOT to use ConfigureAwait(false)

Not all scenarios are suitable for `ConfigureAwait(false)`.
In some cases, it is important to preserve the captured context:

- In user interface code

If you develop code for Windows Forms, WPF, UWP, MAUI, or other UI frameworks, you probably need to update UI elements after asynchronous operations. In such cases, do not use `ConfigureAwait(false)` for operations followed by UI interaction.

```cs
private async void Button_Click(object sender, RoutedEventArgs e)
{
    // We do NOT use ConfigureAwait(false), since we are still working with the UI
    var result = await LoadDataAsync();
    
    // These operations must be performed in the UI thread
    ResultTextBlock.Text = result;
    ProgressBar.Visibility = Visibility.Collapsed;
    
    // Here you can use ConfigureAwait(false), since we are not working with the UI anymore
    await LogOperationAsync().ConfigureAwait(false);
}
```

- In ASP.NET (classic)

In ASP.NET Web Forms and other legacy ASP.NET technologies, the synchronization context stores important information about the current request.
If you use `ConfigureAwait(false)`, you may lose access to `HttpContext.Current` and other properties associated with the request.

```cs
protected async void Page_Load(object sender, EventArgs e)
{
    // Without ConfigureAwait(false) to save the HttpContext
    var userData = await GetUserDataAsync();
    
    // We use HttpContext after await
    if (HttpContext.Current.User.Identity.IsAuthenticated)
    {
        // We show user data
    }
}
```

- In tests that check the context

If you are writing tests that verify that the synchronization context is correct, you should also avoid `ConfigureAwait(false)` in the tests themselves to ensure that the context is captured and restored as expected.

## New features of ConfigureAwait in .NET 8.0

In .NET 8.0, Microsoft extended the functionality of `ConfigureAwait` by adding a new enumeration, `ConfigureAwaitOptions`:

```cs
public enum ConfigureAwaitOptions
{
  /// <summary>No options specified.</summary>
  None = 0,
  /// <summary>Attempts to marshal the continuation back to the original <see cref="T:System.Threading.SynchronizationContext" /> or <see cref="T:System.Threading.Tasks.TaskScheduler" /> present on the originating thread at the time of the await.</summary>
  ContinueOnCapturedContext = 1,
  /// <summary>Avoids throwing an exception at the completion of awaiting a <see cref="T:System.Threading.Tasks.Task" /> that ends in the <see cref="F:System.Threading.Tasks.TaskStatus.Faulted" /> or <see cref="F:System.Threading.Tasks.TaskStatus.Canceled" /> state.</summary>
  SuppressThrowing = 2,
  /// <summary>Forces an await on an already completed <see cref="T:System.Threading.Tasks.Task" /> to behave as if the <see cref="T:System.Threading.Tasks.Task" /> wasn't yet completed, such that the current asynchronous method will be forced to yield its execution.</summary>
  ForceYielding = 4,
}
```

These options provide more flexible control over the behavior of `await`.
Let's consider each of them in more detail.

### None and ContinueOnCapturedContext

These options correspond to the classic `ConfigureAwait(false)` and `ConfigureAwait(true)`:

```cs
// Equivalent calls
await task.ConfigureAwait(false);
await task.ConfigureAwait(ConfigureAwaitOptions.None);

// Equivalent calls
await task;
await task.ConfigureAwait(true);
await task.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
```

It is important to note that by default for a new `ConfigureAwait(ConfigureAwaitOptions)`, unless `ContinueOnCapturedContext` is specified, no context is captured. This is opposite to the behavior of normal `await` without `ConfigureAwait`, where the context is captured by default.

### SuppressThrowing

This option suppresses the exceptions that normally occur when a `await` task terminates with an error:

```cs
// Waiting for a task without throwing an exception
await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

// Equivalent code without using SuppressThrowing
try 
{ 
    await task.ConfigureAwait(false); 
} 
catch 
{ 
    // Ignore the error
}
```

`SuppressThrowing` is especially useful for scenarios where you want to wait for the task to complete regardless of the result.
For example, when canceling an operation, you often need to wait for the task to complete before starting a new operation:

```cs
// Canceling the old task and waiting for it to complete, ignoring exceptions
_cts.Cancel();
await _task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

// Start a new task
_cts = new CancellationTokenSource();
_task = PerformOperationAsync(_cts.Token);
```

> It is important to remember
{: .prompt-info }
`SuppressThrowing` only works with `Task` but not with `Task<T>`. For `Task<T>`, attempting to use `SuppressThrowing` will result in compile error `(CA2261)` and runtime exception `ArgumentOutOfRangeException`. This is because, in the case of an exception, it is not clear which value of type `T` should be returned.

```cs
public new ConfiguredTaskAwaitable<TResult> ConfigureAwait(ConfigureAwaitOptions options)
{
    if ((options & ~(ConfigureAwaitOptions.ContinueOnCapturedContext |
                      ConfigureAwaitOptions.ForceYielding)) != 0)
    {
        ThrowForInvalidOptions(options);
    }

    return new ConfiguredTaskAwaitable<TResult>(this, options);

    static void ThrowForInvalidOptions(ConfigureAwaitOptions options) =>
        throw ((options & ConfigureAwaitOptions.SuppressThrowing) == 0 ?
            new ArgumentOutOfRangeException(nameof(options)) :
            new ArgumentOutOfRangeException(nameof(options), SR.TaskT_ConfigureAwait_InvalidOptions));
}
```

### ForceYielding

This option forces await to always behave asynchronously, even if the task has already completed:

```cs
// Always switches to the pool thread, even if the task has already completed
await task.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
```

Under normal circumstances, if the task has already completed at time `await`, the continuation is executed synchronously in the same thread.
`ForceYielding` changes this behavior by making `await` always act asynchronously, which can be useful for:

- Unit testing of asynchronous code
- Avoiding too deep recursion
- Implementation of asynchronous coordination primitives
- Forced switching of flows

`ForceYielding` is similar to `Task.Yield()`, but with some differences:

- `Task.Yield()` will continue on captured context
- `ForceYielding` does NOT use captured context by default

```cs
// Equivalent calls
await Task.Yield();
await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding | ConfigureAwaitOptions.ContinueOnCapturedContext);
```

This option is particularly useful when you want to ensure that the code after `await` is always executed in a separate message loop, regardless of the state of the task.

## Common errors with ConfigureAwait

Some common errors are common when working with `ConfigureAwait`:

---

- `ConfigureAwait` is NOT a reliable way to avoid deadlocks

```cs
  // Bug: ConfigureAwait is NOT a reliable way to avoid deadlocks
  public string GetData()
  {
      // It can still deadlock if internal
      // methods do not use ConfigureAwait(false)
      return GetDataAsync().ConfigureAwait(false).GetAwaiter().GetResult();
  }
  ```

`ConfigureAwait(false)` helps avoid deadlocks only if ALL code, including code in internal libraries, also uses `ConfigureAwait(false)`.
  Since this cannot be guaranteed, this approach is not a reliable solution to deadlocks.

- ConfigureAwait configures `await`, NOT the task

```cs
  // Error: ConfigureAwait has no effect without await
  public string GetData()
  {
      // ConfigureAwait has no effect here because await is missing!
      return SomethingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
  }

  // Also bug: ConfigureAwait only affects the await it belongs to
  var task = SomethingAsync();
  task.ConfigureAwait(false); // It has no effect!
  await task; // Still continues on the captured context
  ```

`ConfigureAwait` configures the behavior of the `await` statement, not the task itself.
  Calling `ConfigureAwait` without following `await` has no effect.

- ConfigureAwait(false) does not guarantee a thread change

```cs
  // Bug: Thinking that ConfigureAwait(false) always switches the thread
  async Task DoWorkAsync()
  {
      // If the task has already completed, the code will continue execution
      // on the same thread despite ConfigureAwait(false)
      await Task.FromResult(42).ConfigureAwait(false);
      
      // The code here is NOT necessarily executed on another thread!
  }
  ```

`ConfigureAwait(false)` does not guarantee execution on another thread.
  If the task has already completed at time `await`, the code will continue to execute on the same thread, even with `ConfigureAwait(false)`.

---

## Evolution of ConfigureAwait recommendations

Recommendations for using ConfigureAwait(false) have changed over time:

- Initial recommendations
  Early in the implementation of `async/await`, the community recommended using `ConfigureAwait(false)` wherever a captured context was not required. This was due to the frequent deadlocks experienced by early adopters of asynchronous programming and the significant impact `ConfigureAwait(false)` had on application performance.

- Transitional period (2015–2019)
  Over time, the recommendations have become more nuanced:

  - Use `ConfigureAwait(false)` in library code
  - Do not use `ConfigureAwait(false)` in application code

  This simplified the rules and made them more understandable for developers.

- Modern recommendations (with the release of ASP.NET Core)
  ASP.NET Core does not have `SynchronizationContext`, so `ConfigureAwait(false)` has less impact in it. Some libraries have even abandoned the consistent use of `ConfigureAwait(false)` due to:
  - Excessive code noise
  - Less need in environments without SynchronizationContext
  - Higher maintenance complexity

With the release of .NET 8.0 and the new `ConfigureAwait` options, developers have gained more flexible tools for fine-tuning asynchronous behavior.

- General modern consensus
  - Use `ConfigureAwait(false)` in library projects
  - Consider using it in large applications to improve performance
  - In .NET 8.0+ use new options `ConfigureAwait` for specific scenarios
  - In UI applications, be careful with `ConfigureAwait(false)` to avoid losing UI context

## Usage examples

Library for working with data:

```cs
public class DataProcessor
{
    public async Task<ProcessedData> ProcessFileAsync(string filePath)
    {
        // Input/output operation - we use ConfigureAwait(false)
        var fileData = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
        
        // Data processing needs no context
        var processedData = await ProcessBytesAsync(fileData).ConfigureAwait(false);
        
        // Saving data is also context-free
        await SaveToDbAsync(processedData).ConfigureAwait(false);
        
        return processedData;
    }
    
    private async Task<ProcessedData> ProcessBytesAsync(byte[] data)
    {
        // A CPU-intensive operation is run in a separate thread
        return await Task.Run(() => 
        {
            // Data processing
            return new ProcessedData();
        // ConfigureAwait(false) for consistency
        }).ConfigureAwait(false);
    }
    
    private async Task SaveToDbAsync(ProcessedData data)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO...";
        // ... setting parameters
        
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
```

UI application with background processing

```cs
public class MainViewModel : INotifyPropertyChanged
{
    private string _status;
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    
    public async Task LoadDataAsync()
    {
        Status = "Loading...";
        
        try
        {
            // Data loading - does not interact with the UI
            var data = await _dataService.GetDataAsync().ConfigureAwait(false);
            
            // Data processing - does not interact with the UI
            var processedData = await ProcessDataAsync(data).ConfigureAwait(false);
            
            // We return to the UI context to update the interface
            await _dispatcherService.RunOnUIThreadAsync(() =>
            {
                DataItems = new ObservableCollection<DataItem>(processedData);
                Status = "done";
            });
        }
        catch (Exception ex)
        {
            // We return to the UI context to display the error
            await _dispatcherService.RunOnUIThreadAsync(() =>
            {
                Status = $"Error: {ex.Message}";
            });
        }
    }
}
```

## Conclusion

`ConfigureAwait` is an essential tool for optimizing asynchronous code in .NET.

Using it correctly will help you:

- Improve performance by avoiding unnecessary context switches
- Prevent potential deadlocks in complex scenarios
- Create flexible libraries that can work effectively in different environments

In .NET 8.0, with the introduction of new `ConfigureAwaitOptions` options, developers have gained even more control over asynchronous behavior, allowing fine-tuning of code for specific scenarios.

> Remember the main rules:
{: .prompt-info }

- Use `ConfigureAwait(false)` in library code
- Be careful with `ConfigureAwait(false)` in UI code
- `ConfigureAwait` configures `await`, not tasks
- Explore the new capabilities of `ConfigureAwait` in .NET 8.0 for more complex scenarios

By learning the right approaches to asynchronous programming and using `ConfigureAwait`, you can develop high-performance and well-scalable .NET applications that use system resources efficiently and provide an excellent user experience.