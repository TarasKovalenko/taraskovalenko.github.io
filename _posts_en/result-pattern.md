---
title: "Result Pattern in .NET: handling errors without exceptions or null"
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

Handling errors and missing values has always been one of the hardest parts of software development. .NET developers are used to relying on exceptions for error situations and on null for missing data. There's another, functional approach, `Result Pattern`: it explicitly models the successful and unsuccessful outcomes of an operation without exceptions or null values.

## Problems with traditional approach due to exceptions and null

Exceptions in .NET have several significant drawbacks that make code harder to develop and maintain.
Start with performance. Every exception carries information about the call stack, so creating and throwing one is a relatively expensive operation, and frequent exceptions noticeably slow the program down.

Code with exceptions is also harder to read. A method's signature doesn't tell you which exceptions it can throw, so you either document them or go hunting through the code. Sooner or later someone forgets to handle a particular type of error, and the application behaves unpredictably.

Null isn't any better. A returned null can mean no data, a processing error, or simply an uninitialized state. Null checks spread across the codebase, the code gets cumbersome, and `NullReferenceException` still shows up.

Finally, an exception breaks the normal flow of execution: the program "jumps" to the nearest handler, bypassing all the intermediate code. That makes it easy to skip resource cleanup or other logic that was supposed to run.

## What is the Result Pattern and how it simplifies work

`Result Pattern` has its roots in functional programming, particularly in languages like Haskell (Either type) and Rust (Result type). This pattern is based on the principle of "making impossible states impossible" and explicitly modeling the success and failure of operations.
In .NET it caught on along with the wider interest in functional programming and the need for more reliable, better-performing systems.

## When exceptions make life difficult

A typical example is email address validation:

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

The idea is simple: the error becomes part of the data type. Instead of "throwing" the error somewhere in the air (exception), we "return" it as part of the result. The method's signature is honest about the fact that it can fail, and the error gets handled where the method is called.

## The magic of extension methods: How Result gets really powerful

The Result Pattern pays off most when you combine it with extension methods. They let you build chains of data processing steps: each step either passes the value on or stops the chain and returns the error, and the remaining steps are skipped.

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

With LINQ you can process whole collections of results:

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

It works with asynchronous code too:

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

The big one is explicitness. The signature shows the method can return an error, so the code is easier to read and it's harder to forget the error handling. Without exceptions the code runs faster, especially where errors are frequent. Operations chain easily through extension methods. And tests get simpler: there's no exception to catch, you just check the Result properties.

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

The Result Pattern adds another level of abstraction, which can make simple code more complicated. Integrating it with code built on exceptions can be difficult. And you end up with more code than you would with plain exceptions.

### When to use Result Pattern

Use the Result Pattern when:

- Errors are part of the normal flow of execution
- High performance is required
- You want to clearly show that the operation may fail
- It is necessary to chain operations with possible errors

DO NOT use the Result Pattern when:

- Errors are truly exceptional (eg OutOfMemoryException)
- You're integrating with APIs that expect exceptions
- The team is not ready for additional complexity

## Ready-made libraries

You don't have to write your own implementation; there are ready-made libraries:

[CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions) - a popular library with a complete set of functional extensions

[LanguageExt](https://github.com/louthy/language-ext) - functional library with support for Result and much more

[ErrorOr](https://github.com/amantinband/error-or) - a lightweight library for Result Pattern

## Conclusion

`Result Pattern` works best where errors are part of the normal flow of program execution, as in the email validation example. You pay for it with extra complexity and longer code, and that price pays off in large projects where reliable, understandable code matters.

If you want to try `Result Pattern`, start with simple scenarios and expand its use as you gain experience.

> Result Pattern is a tool, not a universal answer. Use it where it actually helps, and mix it with exceptions where they fit better.
{: .prompt-info }