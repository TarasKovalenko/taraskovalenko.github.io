---
title: Result Pattern is an elegant alternative to exceptions and null values
author: Taras Kovalenko
date: 2025-06-27 09:00:00.000000000 +02:00
categories:
- ".net"
- C#
- design patterns
- software architecture
tags:
- ".net"
- C#
- patter
- softwarearchitecture
lang: en
locale: en_US
translation_key: result-pattern
permalink: "/en/posts/result-pattern/"
---

Managing errors and missing values ​​has always been one of the most difficult parts of software development. In the .NET world, developers have traditionally relied on the exceptions mechanism to handle error situations and the null value to indicate the absence of data. However, there is a more elegant and functional approach, `Result Pattern`, which allows you to explicitly model the successful and unsuccessful results of operations without using exceptions or null values.

## Problems with traditional approach due to exceptions and null

Exceptions in .NET, while a powerful mechanism, have several significant drawbacks that can make code difficult to develop and maintain.
First of all, exceptions significantly affect the performance of the program, because creating and throwing an exception requires additional system resources. Each exception contains information about the call stack, which makes its creation a relatively expensive operation.

Also, exceptions make the code harder to read and understand. When a method can throw an exception, it's not always obvious from its signature, forcing developers to constantly document possible exceptions or look for them in the code. This leads to situations where developers forget to handle certain types of errors, which can cause the application to behave unpredictably.

Null values ​​are no less problematic. When a method returns null, it can mean any number of things: no data, a processing error, or simply an uninitialized state. Developers have to constantly check for null, which makes the code cumbersome and prone to `NullReferenceException`.

Another problem is that exceptions disrupt the normal flow of program execution. When an exception occurs, the program "jumps" to the nearest handler, bypassing all the intermediate code. This can cause critical resource cleanup logic or other critical operations to be skipped.

## What is the Result Pattern and how it simplifies work

`Result Pattern` has its roots in functional programming, particularly in languages like Haskell (Either type) and Rust (Result type). This pattern is based on the principle of "making impossible states impossible" and explicitly modeling the success and failure of operations.
In the .NET ecosystem, this approach has gained popularity due to the growing interest in functional programming and the need to create more reliable and performant systems.

## When exceptions make life difficult

Consider a typical scenario - email address validation:

```cs
public void ValidateEmail(string email)
{
    if (string.IsNullOrEmpty(email))
        throw new ArgumentException("Email cannot be empty");
    
    if (!email.Contains("@"))
        throw new ArgumentException("Email must contain the @ symbol");
    
    if (email.Length > 254)
        throw new ArgumentException("Email is too long");
}

// Using
try
{
    ValidateEmail(userEmail);
    Console.WriteLine("Email is valid!");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

Problems with this approach:

- Exceptions are expensive in terms of performance
- It is not clear what exceptions the method can throw
- It is difficult to compose operations
- Exceptions mix business logic with error handling

`Result Pattern` solves these problems by representing the result of the operation as an object that can contain either a success result or an error.

Basic implementation of Result:

```cs
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new Result(true, null);
    public static Result Failure(string error) => new Result(false, error);
}

public class Result<T> : Result
{
    public T Value { get; }

    private Result(bool isSuccess, T value, string error) : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new Result<T>(true, value, null);
    public static new Result<T> Failure(string error) => new Result<T>(false, default(T), error);
}
```

We rewrite the validation with the Result Pattern

```cs
public static Result ValidateEmail(string email)
{
    if (string.IsNullOrEmpty(email))
        return Result.Failure("Email cannot be empty");
    
    if (!email.Contains("@"))
        return Result.Failure("Email must contain the @ symbol");
    
    if (email.Length > 254)
        return Result.Failure("Email is too long");
    
    return Result.Success();
}

// Using
var result = ValidateEmail(userEmail);
if (result.IsSuccess)
{
    Console.WriteLine("Email is valid!");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

The Result Pattern is based on a simple but powerful idea: make errors part of the data type. Instead of "throwing" the error somewhere in the air (exception), we "return" it as part of the result.

Key principles of the Result Pattern:

- Explicit over Implicit - a method clearly indicates that it can return an error
- Errors as data - the error becomes part of the return type
- Composition - easy to chain operations that may fail
- Locality - error handling occurs where the method is called

## The magic of extension methods: How Result gets really powerful

The true power of the Result Pattern is revealed through extension techniques. They allow you to create elegant data processing pipelines, similar to a pipeline in a factory - each operation adds something to the result or stops the pipeline in case of an error.

If something goes wrong at any stage, the conveyor stops and reports the problem.

```cs
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> func)
    {
        if (result.IsFailure)
            return Result<TOut>.Failure(result.Error);
        
        return Result<TOut>.Success(func(result.Value));
    }

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> func)
    {
        if (result.IsFailure)
            return Result<TOut>.Failure(result.Error);
        
        return func(result.Value);
    }

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, string error)
    {
        if (result.IsFailure)
            return result;
        
        if (!predicate(result.Value))
            return Result<T>.Failure(error);
        
        return result;
    }
}
```

## Integration with LINQ

The Result Pattern works great with LINQ, allowing you to process collections of results:

```cs
public static class ResultLinqExtensions
{
    public static Result<IEnumerable<TOut>> SelectMany<TIn, TOut>(
        this IEnumerable<TIn> source, 
        Func<TIn, Result<TOut>> selector)
    {
        var results = new List<TOut>();
        
        foreach (var item in source)
        {
            var result = selector(item);
            if (result.IsFailure)
                return Result<IEnumerable<TOut>>.Failure(result.Error);
            
            results.Add(result.Value);
        }
        
        return Result<IEnumerable<TOut>>.Success(results);
    }
}

// Example: validation of a list of email addresses
var emails = new[] { "user1@example.com", "user2@example.com", "invalid-email" };

var validationResult = emails.SelectMany(email => 
    ValidateEmail(email).Map(_ => email));

if (validationResult.IsSuccess)
{
    Console.WriteLine("All email addresses are valid");
}
else
{
    Console.WriteLine($"Error found: {validationResult.Error}");
}
```

## Asynchronous operations with Result

The Result Pattern works great with asynchronous code:

```cs
public static class AsyncResultExtensions
{
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask, 
        Func<TIn, Task<TOut>> func)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<TOut>.Failure(result.Error);
        
        var value = await func(result.Value);
        return Result<TOut>.Success(value);
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask, 
        Func<TIn, Task<Result<TOut>>> func)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<TOut>.Failure(result.Error);
        
        return await func(result.Value);
    }
}

// Example of use
public async Task<Result<User>> CreateUserAsync(string email, string name, int age)
{
    return await ValidateEmailAsync(email)
        .BindAsync(_ => ValidateNameAsync(name))
        .BindAsync(_ => ValidateAgeAsync(age))
        .MapAsync(_ => SaveUserToDatabase(email, name, age));
}
```

## Advantages of Result Pattern

- Visibility of errors
  The method clearly indicates that it can return an error. This improves code readability and helps developers understand what error handling needs to be done.

- Productivity
  No exceptions means better performance, especially in error-prone scenarios.

- Composition
  Easily chain operations using extension methods, creating elegant data processing pipelines.

- Testing
  Easier to test code because you don't need to catch exceptions - just check the Result properties.

```cs
[Test]
public void ValidateEmail_EmptyEmail_ReturnsFailure()
{
    // Arrange
    var email = string.Empty;
    
    // Act
    var result = ValidateEmail(email);
    
    // Assert
    Assert.That(result.IsFailure, Is.True);
    Assert.That(result.Error, Is.EqualTo("Email cannot be empty"));
}
```

## Disadvantages and limitations

- Additional difficulty
  The Result Pattern adds another level of abstraction that can complicate simple code.
  
- Compatibility with existing code
  Integrating with code that uses exceptions can be difficult.

- Code size
  The code becomes longer compared to simply using exceptions.

### When to use Result Pattern

Use the Result Pattern when:

- Errors are part of the normal flow of execution
- High productivity is required
- You want to clearly show that the operation may fail
- It is necessary to chain operations with possible errors

DO NOT use the Result Pattern when:

- Errors are truly exceptional (eg OutOfMemoryException)
- Integrate with APIs that expect exceptions
- The team is not ready for additional complexity

## Ready-made libraries

Instead of writing your own implementation, consider ready-made libraries:

[CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions) - a popular library with a complete set of functional extensions

[LanguageExt](https://github.com/louthy/language-ext) - functional library with support for Result and much more

[ErrorOr](https://github.com/amantinband/error-or) - a lightweight library for Result Pattern

## Conclusion

`Result Pattern` is a powerful tool for creating reliable and understandable code. It is particularly useful in scenarios where errors are part of the normal flow of program execution.

Key advantages:

- Visibility of errors
- Better performance
- Elegant composition of operations
- Easier testing

Although `Result Pattern` adds some complexity, it pays off in large projects where code reliability and comprehensibility are important. Start with simple scenarios and gradually expand the usage as you gain experience.

> Result Pattern is a tool, not a silver bullet. Use it where it will do the most good, and feel free to combine it with exceptions where appropriate.
{: .prompt-info }