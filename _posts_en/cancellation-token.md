---
title: CancellationToken in C# - usage, issues and best practices
author: Taras Kovalenko
date: 2025-05-16 09:00:00.000000000 +02:00
categories:
- ".net"
- C#
- Threading
tags:
- ".net"
- C#
- threading
- CancellationToken
lang: en
locale: en_US
translation_key: cancellation-token
permalink: "/en/posts/cancellation-token/"
---

## What is CancellationToken?

`CancellationToken` is a data structure in C# .NET that allows elegant cancellation of asynchronous operations. It is a message mechanism that is transmitted between different parts of the code to signal the need to terminate the execution of a certain operation. `CancellationToken` itself is only an object to check the cancellation status, but cannot initiate cancellation.

## What problem does CancellationToken solve?

In the world of asynchronous programming, situations often arise when it is necessary to stop the execution of an operation in progress.
Without a proper cancellation mechanism for asynchronous operations, the following problems can occur:

- Resource leak - asynchronous operations can keep files, network connections, or other system resources open.
- Decreased performance - unnecessary operations continue to run, consuming CPU time and memory.
- Deterioration of user experience - the program does not respond to user requests to stop long-running operations.
- Difficulties with coordination - it is difficult to synchronize the stopping of related operations.
- Failure to handle errors - without an undo mechanism, it is difficult to handle situations where an operation must be aborted due to an error.

`CancellationToken` solves these problems by providing a standardized, cooperative cancellation mechanism that works at all application levels.

## Basic components of the cancellation system

The undo system in .NET consists of three key components:

`CancellationTokenSource` is a class that creates a token and controls the cancellation signal. It has a method `Cancel()` (or the asynchronous variant `CancelAsync()`) that sets the cancellation flag.
`CancellationToken` is a structure that is passed to asynchronous methods. It has a property `IsCancellationRequested` that indicates whether cancellation was requested, and a method `ThrowIfCancellationRequested()` that throws an exception if cancellation was requested.
`OperationCanceledException` - an exception that occurs when the operation is canceled. This is the standard way to signal that an operation has been aborted rather than failing.

## How to use CancellationToken?

```csharp
// Creating a cancellation token source
using CancellationTokenSource cts = new CancellationTokenSource();
CancellationToken token = cts.Token;

try
{
    // Starting an asynchronous operation with the transfer of a cancellation token
    Task task = LongRunningOperationAsync(token);

    // Elsewhere in the code (for example, after clicking the Cancel button)
    await cts.CancelAsync();
    
    // Waiting for the transaction to complete (even if canceled)
    await task;
}
catch (OperationCanceledException)
{
    Console.WriteLine("The operation has been cancelled!");
}
```

A method that supports cancellation might look like this:

```csharp
async Task LongRunningOperationAsync(CancellationToken cancellationToken)
{
    for (int i = 0; i < 100; i++)
    {
        // Check for cancellation - will throw an OperationCanceledException on cancellation
        cancellationToken.ThrowIfCancellationRequested();
        
        // Or an alternative check
        if (cancellationToken.IsCancellationRequested)
        {
            // Perform resource cleanup if necessary
            throw new OperationCanceledException(cancellationToken);
        }
        
        // Delay supporting cancellation
        await Task.Delay(100, cancellationToken);
    }
}
```

### Cancellation by timeout

`CancellationTokenSource` allows you to automatically cancel operations after a certain period of time:

```csharp
// Creating a token source with a timeout of 5 seconds
using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    // The operation will be canceled automatically after 5 seconds
    await LongRunningOperationAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("The operation was canceled due to a timeout!");
}
```

### Merge cancellation tokens

Multiple cancellation tokens can be combined so that the operation is canceled if any of the tokens emits a cancellation signal:

```csharp
using CancellationTokenSource cts1 = new CancellationTokenSource();
using CancellationTokenSource cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));

// Creating a token source that will be revoked if any of the other tokens are revoked
using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

try
{
    await LongRunningOperationAsync(linkedCts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("The operation has been cancelled!");
}
```

## Best practices for using CancellationToken

### Always add the `CancellationToken` parameter to async methods

Each asynchronous method must accept `CancellationToken` as a parameter. This makes it easier to support undo operations throughout the application. Set the default value to `default` to make the parameter optional.

```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken = default)
{
    // Realization
}
```

Don't create methods without support for overrides, as this will make them harder to override in the future.

### Pass the cancellation token to all nested async operations

Passing the cancellation token to all nested asynchronous operations ensures the correct cancellation of the entire chain of operations. This avoids situations where the parent operation is canceled but nested operations continue to execute.

```csharp
public async Task ProcessDataAsync(CancellationToken cancellationToken = default)
{
    var data = await FetchDataAsync(cancellationToken);
    var processedData = await TransformDataAsync(data, cancellationToken);
    await SaveResultAsync(processedData, cancellationToken);
}
```

### Regularly check the cancellation token in long-running operations

In operations with large amounts of data or loops, the cancellation token must be checked regularly. This allows you to quickly respond to a cancellation request and not waste resources on unnecessary work.

```csharp
public async Task ProcessLargeDataSetAsync(IEnumerable<Data> items, CancellationToken cancellationToken = default)
{
    foreach (var item in items)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ProcessItemAsync(item, cancellationToken);
    }
}
```

### Use the using container for `CancellationTokenSource`

`CancellationTokenSource` implements the `IDisposable` interface and must be properly freed. Using the `using` container ensures that resources will be freed even if an exception occurs.

```csharp
using var cts = new CancellationTokenSource();
```

### Handle `OperationCanceledException` properly

When an operation cancels via `CancellationToken`, it normally generates `OperationCanceledException`. It is important to handle this exception correctly, distinguishing between expected cancellation and other errors.

```csharp
try
{
    await DoWorkAsync(token);
}
catch (OperationCanceledException ex) when (ex.CancellationToken == token)
{
    // Pending cancellation
    logger.Information("The operation was canceled as expected");
}
catch (Exception ex)
{
    // Other exceptions are errors that need to be handled
    logger.Error(ex, "An unexpected error occurred");
}
```

### Use cancellations instead of timeouts

Instead of manually setting timeouts with `Task.Delay` or `Task.WhenAny`, use the built-in timeout mechanism in `CancellationTokenSource`. This simplifies the code and ensures correct cancellation of operations.

```csharp
// That's right - with cancellation support
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await DoWorkAsync(cts.Token);

// Incorrect - no cancellation support
var task = DoWorkAsync();
var completed = await Task.WhenAny(task, Task.Delay(5000));
if (completed != task)
{
    // The operation timed out but continues to run in the background!
}
```

### Consider using `IsCancellationRequested` for _soft_ cancellation

In some cases it is better to use the `IsCancellationRequested` check instead of `ThrowIfCancellationRequested`. This allows you to implement _soft_ cancellation, where you can return intermediate results or perform additional actions before terminating.

```csharp
public async Task<IEnumerable<Result>> ProcessBatchAsync(IEnumerable<Data> items, CancellationToken cancellationToken = default)
{
    var results = new List<Result>();
    
    foreach (var item in items)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            // We return intermediate results instead of throwing an exception
            return results;
        }
        
        var result = await ProcessItemAsync(item, cancellationToken);
        results.Add(result);
    }
    
    return results;
}
```

However, it should be noted that with this approach, the task status will be `RanToCompletion`, not `Canceled`. This may affect behavior when using `Task.ContinueWith` or other methods that depend on the status of the task.

### Don't capture cancellation token in closures

Avoid capturing the cancellation token when using lambda expressions or anonymous methods. Instead, pass it as a parameter.

```csharp
// Incorrect - the token is captured in the lock
CancellationToken token = cts.Token;
Task.Run(() => 
{
    // Captured token
    while (!token.IsCancellationRequested) 
    {
        // Work
    }
});

// That's right - the token is passed as a parameter
Task.Run(() => 
{
    // Work

    // Passing a token as a parameter
}, cts.Token);
```

### Use `TaskCompletionSource` with cancellation token

When working with `TaskCompletionSource`, register a cancellation token to properly cancel the task.

```csharp
public Task<T> CreateCancellableTask<T>(CancellationToken cancellationToken)
{
    var tcs = new TaskCompletionSource<T>();
    
    // We register the cancellation
    cancellationToken.Register(() => 
        tcs.TrySetCanceled(cancellationToken),
        useSynchronizationContext: false
    );
    
    // We use tcs to set the result or error
    
    return tcs.Task;
}
```

### Set reasonable time limits for cancellations

Depending on the type of operation, set the appropriate timeouts:

```csharp
// For API requests
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// For long running background operations
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// For short operations
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
```

## Using CancellationToken in ASP.NET Core

In ASP.NET Core, each HTTP request receives its own cancellation token, which is automatically canceled if the client closes the connection. This allows you to elegantly stop processing requests when they are no longer needed.

```csharp
// Controller
[HttpGet]
public async Task<IActionResult> GetDataAsync(CancellationToken cancellationToken)
{
    // The token will be revoked if the user closes the connection
    var data = await _dataService.GetDataAsync(cancellationToken);
    return Ok(data);
}

// Service
public class DataService(HttpClient httpClient) : IDataService
{    
    public async Task<Data> GetDataAsync(CancellationToken cancellationToken)
    {
        // We pass the token to HttpClient
        var response = await httpClient.GetAsync("api/data", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<Data>(cancellationToken: cancellationToken);
    }
}
```

## Real examples of using CancellationToken

### Canceling HTTP requests in HttpClient

`CancellationToken` is particularly useful when dealing with `HTTP` queries where the user may decide to cancel the download operation:

```csharp
public async Task<string> GetWebContentAsync(string url, CancellationToken cancellationToken = default)
{
    using HttpClient client = new HttpClient();
    
    // We set the timeout on request
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken, timeoutCts.Token);
    
    try
    {
        // We use the combined token for the request
        HttpResponseMessage response = await client.GetAsync(url, linkedCts.Token);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync(linkedCts.Token);
    }
    catch (OperationCanceledException ex)
    {
        if (timeoutCts.Token.IsCancellationRequested)
            throw new TimeoutException($"A request to {url} timed out", ex);
        
        // Other cancellation (e.g. by the user)
        throw;
    }
}
```

### Parallel data processing with the possibility of cancellation

```csharp
public async Task ProcessFilesAsync(string[] filePaths, CancellationToken cancellationToken = default)
{
    // We create a list of tasks
    var tasks = new List<Task>();
    
    foreach (var filePath in filePaths)
    {
        // We check the cancellation before starting a new task
        cancellationToken.ThrowIfCancellationRequested();
        
        tasks.Add(ProcessFileAsync(filePath, cancellationToken));
    }
    
    try
    {
        // We are waiting for the completion of all tasks with the possibility of cancellation
        await Task.WhenAll(tasks);
    }
    catch (OperationCanceledException)
    {
        // We log the cancellation and try to save the intermediate results
        Console.WriteLine("File processing canceled.");
        
        // Here you can save intermediate results
    }
}

private async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
{
    // Implementation of file processing with periodic cancellation check
}
```

### Implementation of periodic background tasks with cancellation support

```csharp
public class BackgroundWorker : IDisposable
{
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private Task _workerTask;
    
    public void Start()
    {
        _workerTask = DoWorkAsync(_cts.Token);
    }
    
    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // We perform a periodic task
                await PerformWorkAsync(cancellationToken);
                
                // We are waiting for the next cycle with the possibility of cancellation
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Pending cancellation
                break;
            }
            catch (Exception ex)
            {
                // We log an error, but continue work
                Console.WriteLine($"Background task error: {ex.Message}");
                
                // A short pause before the next attempt
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
    
    private async Task PerformWorkAsync(CancellationToken cancellationToken)
    {
        // Implementation of work with periodic check of cancellation
    }
    
    public void Stop()
    {
        _cts.Cancel();
    }
    
    public async Task StopAndWaitAsync(TimeSpan timeout)
    {
        _cts.Cancel();
        
        // We are waiting for the completion of the task with a timeout
        using var timeoutCts = new CancellationTokenSource(timeout);
        
        try
        {
            await _workerTask.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            Console.WriteLine("Could not wait for background task to complete");
        }
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
```

## Important notes about using CancellationToken

- Cancellation is cooperative. Transactions do not stop automatically - they must periodically check for and respond to the cancellation token. This means that code that does not validate the token will not be invalidated.
- Cancellation does not mean immediate termination. After `Cancel()` is called, operations can continue until the cancellation token is checked. This allows operations to complete correctly.
- `CancellationTokenSource` is consuming resources. Always use `using` or call `Dispose()` after use to avoid resource leaks.
- The cancellation token should be passed rather than created at each level. Create `CancellationTokenSource` at the top level of the call hierarchy, then pass the token down the call chain.
- Cancellation should be quick. Methods should not perform time-consuming operations after detection of cancellation. They should clean up resources and complete as quickly as possible.

## Conclusion

`CancellationToken` is a powerful and flexible mechanism for managing the lifecycle of asynchronous operations in C# .NET. It allows you to elegantly cancel operations when they are no longer needed, avoiding resource leaks and improving application performance.
By following these best practices, you can effectively use `CancellationToken` in your projects, creating reliable and efficient asynchronous applications. Correct use of the cancellation mechanism is especially important in server applications where efficient use of resources is critical to scalability and performance.
The cancellation mechanism using `CancellationToken` is the recommended approach in modern C# development because it:

- Provides a standardized cancellation mechanism
- Supported by most .NET libraries and frameworks
- Integrates with other asynchronous APIs
- Allows graceful handling of cancellations at all application levels
- Improves the overall reliability and efficiency of the program

By using `CancellationToken` in all asynchronous methods, you create code that is easier to maintain, extend, and test.