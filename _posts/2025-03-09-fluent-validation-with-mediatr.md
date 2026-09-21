---
title: "Валідація запитів із FluentValidation, MediatR та IResult"
author: Taras Kovalenko
date: 2025-03-09 09:00:00 +0200
categories: [.net, C#, performance, software architecture, GC]
tags: [.net, C#, FluentValidation, Minimal API, pipeline, clean architecture, patter]
---

У .NET додатках валідацію запитів часто будують на зв'язці `FluentValidation` та `MediatR`.
Традиційно, коли валідація не проходить, ми викидаємо виняток `ValidationException`.
Це працює, але здебільшого не найефективніше.
Альтернатива - повертати `IResult`: це швидше і витрачає менше пам'яті.

Патерн `CQRS` (Command Query Responsibility Segregation) разом із бібліотекою `MediatR` дає чисту й підтримувану архітектуру.
`FluentValidation` додає до неї перевірку вхідних даних ще до того, як вони потраплять у бізнес-логіку.
Але стандартний підхід із винятками має обмеження, і у високонавантажених системах вони відчутні.
Починаючи з версії 6.0, `.NET` має `Minimal API` та інтерфейс `IResult`, який став стандартним способом повертати HTTP-відповіді.
Він дозволяє керувати відповідями без повноцінних контролерів.
Разом із `MediatR` та `FluentValidation` з нього виходить валідація, яку легше читати і яка помітно швидша.

Перехід від винятків до `IResult` змінює сам підхід до обробки помилок у веб-додатку.
Винятки призначені для "виняткових" ситуацій, а невдала валідація - звичайний, очікуваний результат, частина нормального потоку виконання.
Найбільше це відчувається в мікросервісах, де валідація відбувається на кількох рівнях: у клієнтському додатку, в API-шлюзі, в окремих мікросервісах. Кожен викинутий і оброблений виняток у такому ланцюжку навантажує систему, а функціональний підхід з IResult цього навантаження не створює.

## Традиційний підхід з винятками

Спочатку традиційний підхід із винятками:

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

### Проблеми з підходом на основі винятків

Викидати винятки при помилках валідації - поширена практика, але в неї є три недоліки:

1. Вплив на продуктивність
Викинути й обробити виняток у .NET значно дорожче, ніж пройти звичайним шляхом виконання. Треба створити об'єкт винятку, який потім прибирає збирач сміття. Треба захопити й розгорнути стек викликів (`stack unwinding`), а це набагато повільніше за звичайний код. До того ж JIT гірше оптимізує код з обробкою винятків, і на гарячих шляхах виконання це помітно.

2. Високе споживання пам'яті
Разом із винятком .NET захоплює повний стек викликів: методи в ланцюжку, значення параметрів і локальних змінних, контекст виконання. Це займає чимало пам'яті. Якщо навантаження високе або валідація часто не проходить, збирач сміття працює частіше, і під піковим навантаженням програма помітно сповільнюється.

3. Негативний вплив на читабельність коду
Винятки призначені для дійсно виняткових ситуацій, а не для керування звичайним потоком виконання. Валідація - очікувана частина роботи програми, тож винятки для неї цей принцип порушують. Код із купою блоків `try-catch` під різні результати валідації важче читати, підтримувати й відлагоджувати, бо потік виконання стає неочевидним.

## Покращений підхід з використанням IResult

Інтерфейс `IResult` з `.NET Minimal API` дозволяє повертати різні типи HTTP-відповідей без винятків.

Ось реалізація:

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

### Переваги підходу з IResult

1. Вища продуктивність
`IResult` прибирає всі витрати, пов'язані з винятками: немає розгортання стеку викликів, не треба створювати й обробляти об'єкти винятків, а JIT може застосувати оптимізації, яким обробка винятків заважає. Найпомітніша різниця під високим навантаженням і на гарячих шляхах.

2. Менше споживання пам'яті
Повертаючи результат через `IResult`, ви не захоплюєте стек викликів і не створюєте об'єктів винятків з їхніми метаданими. Алокацій менше, збирач сміття працює рідше, а отже менше пауз на збирання сміття і проблем із фрагментацією пам'яті.

3. Чіткіший потік контролю
З `IResult` потік виконання явний: замість того щоб переривати його винятком, ви повертаєте конкретний тип HTTP-відповіді. Код стає лінійнішим, в обробниках не потрібні численні блоки try-catch, а тестувати й відлагоджувати простіше, бо результат методу чітко визначений.

4. Гнучкість у поверненні HTTP-відповідей
З `IResult` легко повернути саме ту відповідь, яка потрібна: `BadRequest`, коли дані не пройшли валідацію, або `Forbidden`, коли проблема з авторизацією чи доступом. Тіло відповіді теж можна налаштувати, наприклад додати конкретні повідомлення про помилки валідації, щоб користувач бачив, що саме виправити.

## Реєстрація поведінки валідації

Щоб увімкнути підхід на базі `IResult`, зареєструйте поведінку в DI-контейнері:

```cs
// Реєстрація валідаторів
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationResultBehavior<,>));
});
```

## Приклад використання

```cs
// Запит
public record CreateUserCommand(string Username, string Email) : IRequest<IResult>;

// Валідатор
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
        // Логіка створення користувача...
        return Results.Created($"/users/{userId}", new UserDto { /* ... */ });
    }
}
```

## Детальніше про помилки валідації

Якщо клієнту потрібна детальніша інформація про помилки валідації, створіть окремий клас для відповіді:

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

І використовувати цей клас у поведінці валідації:

```cs
if (!validationResult.IsValid)
{
    return Results.BadRequest(new ValidationProblemResponse(validationResult));
}
```

## Висновок

Зв'язка `IResult`, `FluentValidation` та `MediatR` найбільше виграє у високонавантажених системах: ви не платите за викидання винятків, менше навантажуєте пам'ять і збирач сміття, а обробники читаються простіше.

Клієнт при цьому нічого не втрачає: помилки валідації приходять як звичайні відповіді `IResult` з інформативним тілом, без жодного `ValidationException` усередині.
