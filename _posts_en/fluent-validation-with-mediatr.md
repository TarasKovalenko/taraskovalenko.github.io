---
title: FluentValidation + MediatR using IResult is an effective approach
author: Taras Kovalenko
date: 2025-03-09 09:00:00.000000000 +02:00
categories:
- ".net"
- C#
- performance
- software architecture
- GC
tags:
- ".net"
- C#
- FluentValidation
- Minimal API
- pipeline
- clean architecture
- patter
lang: en
locale: en_US
translation_key: fluent-validation-with-mediatr
permalink: "/en/posts/fluent-validation-with-mediatr/"
---

In modern .NET applications, the combination of `FluentValidation` and `MediatR` has become a popular approach to implement query validation.
Traditionally, when validation fails, we throw the exception `ValidationException`.
However, in many cases this is not the most effective approach.
In this article, we'll look at an alternative method using `IResult` that improves performance and reduces memory usage.

The `CQRS` (Command Query Responsibility Segregation) pattern combined with the `MediatR` library allows you to create a clean and maintainable application architecture.
Adding validation with `FluentValidation` makes this approach even more powerful, allowing you to validate input data before it even reaches the business logic.
But the standard exception approach has its limitations, especially in heavily loaded systems.
`.NET` since version 6.0 introduced the `Minimal API` concept and the `IResult` interface, which became part of the standard approach to returning HTTP responses.
This interface provides a simple yet powerful way to manage responses without the need for full controllers.
By combining it with `MediatR` and `FluentValidation`, we can create an elegant query validation solution that not only improves code readability, but also greatly improves system performance.

The move from throwing exceptions to returning results via `IResult` is not just a syntax change, but a fundamental rethinking of the approach to error handling in web applications.
Instead of using exceptions, which are inherently designed to handle "exceptional" situations, we move to a paradigm where validation is an integral part of the normal flow of program execution, and the result of validation is an expected and predictable result, not something exceptional.
This approach is especially important in microservice architectures, where validation often occurs at several levels: in the client application, in the API gateway, in individual microservices. Each exception thrown and handled in such a chain creates significant system overhead that can be avoided by using a functional approach with IResult.

## Traditional approach with exceptions

First, let's look at the traditional approach of using exceptions:

```cs
public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
     where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v =>
                    v.ValidateAsync(context, cancellationToken)));
            var failures = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .ToList();
            if (failures.Any())
                throw new ValidationException(failures);
        }
        return await next();
    }
}
```

### Problems with the exception-based approach

Although it is common practice to use exceptions to handle validation errors, this approach has several significant drawbacks:

1. Impact on productivity
Exception generation and handling in .NET is a relatively expensive operation compared to the normal flow of code execution. Every time the system throws an exception, .NET must create a new exception object, which is subsequently handled by the garbage collector, which puts additional stress on the memory management system. Also, the process of throwing an exception requires capturing and unrolling the call stack (`stack unwinding`), which is a much slower operation than normal code execution. The JIT compiler also has difficulty optimizing exception-handling code, which can reduce overall program performance, especially at critical parts of the execution path.

2. High memory consumption
When an exception is thrown in a program, .NET automatically captures the full call stack, which includes detailed information about all methods in the call chain, values of passed parameters and local variables, and detailed execution context at the time the exception occurred. This information takes up a significant amount of memory, which becomes especially problematic in high-load systems or scenarios where validation often fails. Frequent creation of exception objects increases the load on the process of memory management and garbage collection, which can lead to noticeable slowdowns of the program during peak loads.

3. Negative impact on code readability
Code that relies on exceptions to control the flow of business logic execution often becomes less understandable and more logically convoluted. Exceptions, by their very nature, are designed to handle truly exceptional situations, not to control the standard flow of program execution. Using them for validation, which is an expected part of normal program operation, violates this principle. Code that contains many `try-catch` blocks to handle different validation results becomes more difficult to understand, maintain, and debug because the flow of program execution becomes less obvious and predictable.

## Improved approach using IResult

The `IResult` interface with `.NET Minimal API` provides an elegant way to return different types of HTTP responses without using exceptions.

Consider an implementation that uses this approach:

```cs
public class ValidationResultBehavior<TRequest, TResult>(IServiceProvider serviceProvider)
    : IPipelineBehavior<TRequest, IResult>
    where TRequest : notnull
    where TResult : notnull, IResult
{
    public async Task<IResult> Handle(
        TRequest request,
        RequestHandlerDelegate<IResult> next,
        CancellationToken cancellationToken
    )
    {
        var validator = serviceProvider.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return await next();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorCode =
                validationResult.Errors.FirstOrDefault()?.ErrorCode
                ?? StatusCodes.Status400BadRequest.ToString();

            return (
                int.TryParse(errorCode, out var code) ? code : StatusCodes.Status400BadRequest
            ) switch
            {
                StatusCodes.Status403Forbidden => Results.Problem(
                    new ForbiddenResponse(
                        validationResult.Errors.FirstOrDefault()?.ErrorMessage
                            ?? "Validation failed"
                    )
                ),
                _ => Results.BadRequest(
                    new BadRequestResponse(
                        validationResult.Errors.FirstOrDefault()?.ErrorMessage
                            ?? "Validation failed"
                    )
                ),
            };
        }

        return await next();
    }
}
```

### Advantages of the IResult approach

1. Higher productivity
Using `IResult` instead of exceptions significantly improves program performance because it completely eliminates the overhead associated with exception generation and handling. When using `IResult`, the system does not need to perform the expensive call stack unwinding operation that normally occurs when an exception is thrown. In addition, this approach prevents the need to create and handle exception objects, which in turn reduces the load on the system. The JIT compiler also gets the chance to better optimize the code, since exception handling often prevents many optimizations that the compiler could have applied. As a result, the program runs faster and more efficiently, especially under high load conditions or on critical execution paths.

2. Less memory consumption
Returning validation results through the `IResult` interface instead of throwing exceptions results in significantly less memory usage in the program. With this approach, the system completely avoids the need to capture and store the entire call stack, which is one of the main reasons for the high memory consumption of exception handling. Not having to create exception objects and their associated metadata also reduces the total number of memory allocations. As a result, the garbage collector works less intensively, which positively affects the overall performance of the application and reduces the likelihood of problems related to memory fragmentation or garbage collection pauses during the operation of the application.

3. Clearer control flow
Using `IResult` significantly improves code readability and makes the flow of program execution more explicit and predictable. Instead of interrupting the normal flow of execution with exceptions, this approach allows specific types of HTTP responses to be returned, making the code more linear and understandable. The error handling logic in controllers or request handlers is greatly simplified, as there is no need to write numerous try-catch blocks to catch and handle exceptions. It also facilitates the process of testing and debugging the application, as the results of the execution of the methods become more predictable and well-defined.

4. Flexibility in returning HTTP responses
The approach using `IResult` gives developers much more flexibility in shaping responses to HTTP requests. The developer gets the ability to easily return different types of HTTP responses depending on the specific context and validation results. For example, it is easy to return a `BadRequest` response when user data does not meet validation requirements, or a `Forbidden` response when there are authorization or access problems. In addition, the developer has the flexibility to customize the response body to provide detailed and useful information to the client side of the application, such as including specific validation error messages that help users understand exactly what needs to be fixed.

## Log validation behavior

To use our new IResult-based approach, you need to register the behavior in the DI container:

```cs
// Registration of validators
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationResultBehavior<,>));
});
```

## Usage example

```cs
// Request
public record CreateUserCommand(string Username, string Email) : IRequest<IResult>;

// Validator
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3)
            .WithErrorCode(StatusCodes.Status400BadRequest.ToString());

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithErrorCode(StatusCodes.Status400BadRequest.ToString());
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, IResult>
{
    public async Task<IResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // User creation logic...
        return Results.Created($"/users/{userId}", new UserDto { /* ... */ });
    }
}
```

## More about validation errors

You may want to provide more details about validation errors.
To do this, you can create a special class for the response:

```cs
public class ValidationProblemResponse
{
    public string Title { get; } = "Validation failed";
    public int Status { get; }
    public IDictionary<string, string[]> Errors { get; }

    public ValidationProblemResponse(ValidationResult validationResult, int status = StatusCodes.Status400BadRequest)
    {
        Status = status;
        
        Errors = validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );
    }
}
```

And use this class in the validation behavior:

```cs
if (!validationResult.IsValid)
{
    return Results.BadRequest(new ValidationProblemResponse(validationResult));
}
```

## Conclusion

Using the `IResult`-based approach with `FluentValidation` and `MediatR` provides significant benefits in terms of performance, memory consumption, and code clarity compared to the traditional exception-based approach. This approach is particularly useful in highly loaded systems where efficiency is critical.

Instead of throwing exceptions that negatively impact performance, we can use `IResult` response types to elegantly handle validation errors while providing informative feedback to clients.
Thus, we get a more reliable and efficient process of validating requests in our application.