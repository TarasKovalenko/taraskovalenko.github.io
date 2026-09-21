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

`CancellationToken` is a struct in C# .NET that lets you cancel asynchronous operations. You pass it between parts of your code as a signal that a particular operation should stop. `CancellationToken` doesn't start a cancellation itself; it only lets you check whether one has been requested.

## What problem does CancellationToken solve?

In async code you regularly need to stop an operation that's already running. Without a way to cancel it, the operation keeps files, network connections and other system resources open, and it burns CPU time and memory on work nobody needs anymore. The user asks to stop a long-running operation, and the program doesn't respond.

There are less obvious costs too. It's hard to stop several related operations together, and just as hard to abort an operation because something else failed.

`CancellationToken` gives you a standard, cooperative cancellation mechanism that works at every level of the application.

## Basic components of the cancellation system

Cancellation in .NET is built from three components:

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

Every async method should accept a `CancellationToken` parameter, so cancellation can flow through the whole application. Give it a default value of `default` and the parameter becomes optional.

```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken = default)
{
    // Realization
}
```

A method without that parameter will need rework the moment someone has to cancel it.

### Pass the cancellation token to all nested async operations

If the token doesn't reach the nested calls, the parent operation gets canceled while the nested ones keep running. Pass the `CancellationToken` into every nested async operation and the whole chain cancels together.

```csharp
public async Task ProcessDataAsync(CancellationToken cancellationToken = default)
{
    var data = await FetchDataAsync(cancellationToken);
    var processedData = await TransformDataAsync(data, cancellationToken);
    await SaveResultAsync(processedData, cancellationToken);
}
```

### Regularly check the cancellation token in long-running operations

A loop or a pass over a large dataset can run for a long time without hitting a single async call, so check the token yourself at regular intervals. That way the operation notices the cancellation quickly and doesn't waste resources on work nobody needs.

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

`CancellationTokenSource` implements `IDisposable`, so it has to be disposed. With `using`, that happens even when an exception is thrown.

```csharp
using var cts = new CancellationTokenSource();
```

### Handle `OperationCanceledException` properly

An operation canceled via `CancellationToken` normally throws `OperationCanceledException`. Handle it separately from other exceptions: an expected cancellation and a real error are different situations.

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

Don't build timeouts by hand with `Task.Delay` or `Task.WhenAny`: `CancellationTokenSource` already has one built in. The code gets simpler, and the operation actually gets canceled.

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

Sometimes checking `IsCancellationRequested` works better than calling `ThrowIfCancellationRequested`. That gives you _soft_ cancellation: the method can return intermediate results or do some extra work before it finishes.

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

Keep in mind that with this approach the task status will be `RanToCompletion`, not `Canceled`. That changes how `Task.ContinueWith` and other methods that look at the task status behave.

### Don't capture cancellation token in closures

In lambdas and anonymous methods, don't capture the token; pass the `CancellationToken` in as a parameter.

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

A task created through `TaskCompletionSource` knows nothing about the token. Register a callback on the token that cancels it:

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

The right timeout depends on the kind of operation:

```csharp
// For API requests
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// For long running background operations
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// For short operations
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
```

## Using CancellationToken in ASP.NET Core

In ASP.NET Core, each HTTP request receives its own cancellation token, which is automatically canceled if the client closes the connection. So you can stop processing a request as soon as nobody needs the result.

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

With `HTTP` requests you need `CancellationToken` when the user can cancel a download:

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

- Cancellation is cooperative. Operations don't stop automatically; they have to check the cancellation token periodically and react to it. Code that never checks the token won't be canceled.
- Cancellation does not mean immediate termination. After `Cancel()` is called, operations can continue until the cancellation token is checked. This allows operations to complete correctly.
- `CancellationTokenSource` holds resources. Always use `using` or call `Dispose()` after use to avoid resource leaks.
- The cancellation token should be passed rather than created at each level. Create `CancellationTokenSource` at the top level of the call hierarchy, then pass the token down the call chain.
- Cancellation should be quick. Methods should not perform time-consuming operations after detection of cancellation. They should clean up resources and complete as quickly as possible.

## Conclusion

`CancellationToken` is the standard way to cancel work in C# .NET: most libraries and frameworks support it, and it plugs into the other async APIs. But it's cooperative. It only works when your code passes the token along and checks it regularly, and a method that doesn't accept a `CancellationToken` can't be canceled from outside.

In practice a few habits cover most of it: accept the token in every async method, pass it into nested calls, check it in long loops, and dispose the `CancellationTokenSource`. Server applications gain the most. A request nobody is waiting for stops holding resources, and that feeds directly into scalability and performance.