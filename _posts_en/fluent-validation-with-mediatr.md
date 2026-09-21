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

In .NET applications, request validation is often built on `FluentValidation` and `MediatR`.
Traditionally, when validation fails, we throw a `ValidationException`.
It works, but it's usually not the most efficient option.
The alternative is to return `IResult`: it's faster and uses less memory.

The `CQRS` (Command Query Responsibility Segregation) pattern combined with the `MediatR` library gives you a clean, maintainable architecture.
`FluentValidation` adds input validation on top, before the data reaches the business logic.
But the standard exception-based approach has limits, and in high-load systems you feel them.
Since version 6.0, `.NET` has had `Minimal API` and the `IResult` interface, which became the standard way to return HTTP responses.
It lets you shape responses without full controllers.
Put it together with `MediatR` and `FluentValidation` and you get request validation that's easier to read and noticeably faster.

Moving from exceptions to `IResult` changes how you handle errors in a web application.
Exceptions are meant for "exceptional" situations, and a failed validation isn't one: it's an expected result and part of the normal flow of execution.
You feel this most in microservices, where validation happens at several levels: in the client application, in the API gateway, in individual microservices. Every exception thrown and handled in that chain adds load, and a functional approach with IResult doesn't.

## Traditional approach with exceptions

First, the traditional approach with exceptions:

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

Throwing exceptions for validation errors is common practice, but it has three drawbacks:

1. Performance cost
Throwing and handling an exception in .NET costs far more than the normal execution path. .NET has to create an exception object, which the garbage collector later cleans up. It has to capture and unwind the call stack (`stack unwinding`), which is much slower than ordinary code. On top of that, the JIT has a harder time optimizing exception-handling code, and on hot paths you notice it.

2. High memory consumption
Along with the exception, .NET captures the full call stack: the methods in the chain, parameter and local variable values, and the execution context. That takes a fair amount of memory. Under high load, or when validation fails often, the garbage collector runs more often and the application slows down noticeably at peak times.

3. Negative impact on code readability
Exceptions are meant for truly exceptional situations, not for controlling the normal flow of execution. Validation is an expected part of how the program works, so using exceptions for it breaks that principle. Code full of `try-catch` blocks for different validation results is harder to read, maintain and debug, because the flow of execution is no longer obvious.

## Improved approach using IResult

The `IResult` interface from `.NET Minimal API` lets you return different types of HTTP responses without exceptions.

Here's the implementation:

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

1. Better performance
`IResult` removes all the costs that come with exceptions: no call stack unwinding, no exception objects to create and handle, and the JIT can apply optimizations that exception handling gets in the way of. The difference is most visible under high load and on hot paths.

2. Less memory consumption
When you return the result through `IResult`, you don't capture the call stack or create exception objects and their metadata. Fewer allocations means the garbage collector runs less often, with fewer GC pauses and fewer memory fragmentation problems.

3. Clearer control flow
With `IResult` the flow of execution is explicit: instead of interrupting it with an exception, you return a specific type of HTTP response. The code is more linear, handlers don't need lots of try-catch blocks, and testing and debugging get easier because each method's result is clearly defined.

4. Flexibility in returning HTTP responses
With `IResult` it's easy to return exactly the response you need: `BadRequest` when the data fails validation, or `Forbidden` when there's an authorization or access problem. You can also customize the response body, for example by including specific validation error messages so users can see what to fix.

## Log validation behavior

To turn on the `IResult`-based approach, register the behavior in the DI container:

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

If the client needs more detail about validation errors, create a dedicated response class:

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

Combining `IResult`, `FluentValidation` and `MediatR` pays off most in high-load systems: you don't pay for throwing exceptions, you put less pressure on memory and the garbage collector, and handlers are easier to read.

The client doesn't lose anything: validation errors come back as ordinary `IResult` responses with an informative body, with no `ValidationException` involved.