---
title: Result Pattern - елегантна альтернатива винятками та null-значенням
author: Taras Kovalenko
date: 2025-06-27 09:00:00 +0200
categories: [.net, C#, design patterns, software architecture]
tags: [.net, C#, patter, softwarearchitecture]
---

Обробка помилок і відсутніх значень завжди була однією з найскладніших частин розробки. .NET-розробники звикли покладатися на винятки (exceptions) для помилкових ситуацій і на null-значення для відсутніх даних. Є інший, функціональний підхід - `Result Pattern`: він явно моделює успішний і неуспішний результат операції без винятків і null-значень.

## Проблеми з традиційним підходом через винятки та null

У винятків у .NET є кілька суттєвих недоліків, які ускладнюють розробку та підтримку коду.
Почнімо з продуктивності. Кожен виняток містить інформацію про стек викликів, тому створити й викинути exception - відносно дорога операція, і часті винятки помітно гальмують програму.

Код з винятками ще й гірше читається. З сигнатури методу не видно, які винятки він може викинути, тож їх доводиться або документувати, або шукати в коді. Врешті якийсь тип помилки забувають обробити, і програма поводиться непередбачувано.

З null не краще. Повернений null може означати відсутність даних, помилку в обробці або просто неініціалізований стан. Перевірки на null розповзаються по всьому коду, він стає громіздким, а `NullReferenceException` однаково трапляється.

І нарешті, виняток перериває нормальний потік виконання: програма "стрибає" до найближчого обробника, минаючи весь проміжний код. Так легко пропустити очищення ресурсів чи іншу логіку, яка мала виконатися.

## Що таке Result Pattern та як він спрощує роботу

`Result Pattern` має коріння у функціональному програмуванні, зокрема в мовах як Haskell (тип Either) та Rust (тип Result). Цей паттерн базується на принципі "зробити неможливі стани неможливими" та явному моделюванні успіху та неуспіху операцій.
У .NET він став популярним разом із загальним інтересом до функціонального програмування і потребою в надійніших і продуктивніших системах.

## Коли винятки ускладнюють життя

Типовий приклад - валідація email адреси:

```cs
public void ValidateEmail(string email)
{
    if (string.IsNullOrEmpty(email))
        throw new ArgumentException("Email не може бути порожнім");
    
    if (!email.Contains("@"))
        throw new ArgumentException("Email має містити символ @");
    
    if (email.Length > 254)
        throw new ArgumentException("Email занадто довгий");
}

// Використання
try
{
    ValidateEmail(userEmail);
    Console.WriteLine("Email валідний!");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Помилка: {ex.Message}");
}
```

Проблеми цього підходу:

- Винятки дорогі з точки зору продуктивності
- Неясно, які саме винятки може кинути метод
- Складно композувати операції
- Винятки змішують бізнес-логіку з обробкою помилок

`Result Pattern` вирішує ці проблеми, представляючи результат операції як об'єкт, який може містити або успішний результат, або помилку.

Базова реалізація Result:

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

Переписуємо валідацію з Result Pattern

```cs
public static Result ValidateEmail(string email)
{
    if (string.IsNullOrEmpty(email))
        return Result.Failure("Email не може бути порожнім");
    
    if (!email.Contains("@"))
        return Result.Failure("Email має містити символ @");
    
    if (email.Length > 254)
        return Result.Failure("Email занадто довгий");
    
    return Result.Success();
}

// Використання
var result = ValidateEmail(userEmail);
if (result.IsSuccess)
{
    Console.WriteLine("Email валідний!");
}
else
{
    Console.WriteLine($"Помилка: {result.Error}");
}
```

Ідея проста: помилка стає частиною типу даних. Замість того, щоб "кидати" помилку кудись у повітря (exception), ми "повертаємо" її як частину результату. Сигнатура методу чесно показує, що він може не вдатися, а помилку обробляють там, де метод викликали.

## Магія методів розширення: Як Result стає справді потужним

Найбільше Result Pattern дає в поєднанні з методами розширення. З них складаються ланцюжки обробки даних: кожен крок або передає значення далі, або зупиняє ланцюжок і повертає помилку, а решта кроків просто пропускається.

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

## Інтеграція з LINQ

З LINQ можна обробляти цілі колекції результатів:

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

// Приклад: валідація списку email адрес
var emails = new[] { "user1@example.com", "user2@example.com", "invalid-email" };

var validationResult = emails.SelectMany(email => 
    ValidateEmail(email).Map(_ => email));

if (validationResult.IsSuccess)
{
    Console.WriteLine("Всі email адреси валідні");
}
else
{
    Console.WriteLine($"Знайдено помилку: {validationResult.Error}");
}
```

## Асинхронні операції з Result

З асинхронним кодом Result Pattern теж працює:

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

// Приклад використання
public async Task<Result<User>> CreateUserAsync(string email, string name, int age)
{
    return await ValidateEmailAsync(email)
        .BindAsync(_ => ValidateNameAsync(name))
        .BindAsync(_ => ValidateAgeAsync(age))
        .MapAsync(_ => SaveUserToDatabase(email, name, age));
}
```

## Переваги Result Pattern

Головне - явність. З сигнатури методу видно, що він може повернути помилку, тож код легше читати і важче забути про обробку помилки. Без винятків код працює швидше, особливо там, де помилки трапляються часто. Операції легко ланцюжити через методи розширення. А тести простіші: не треба ловити винятки, досить перевірити властивості Result.

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
    Assert.That(result.Error, Is.EqualTo("Email не може бути порожнім"));
}
```

## Недоліки та обмеження

Result Pattern додає ще один рівень абстракції, і простий код від цього може стати складнішим. Інтегрувати його з кодом, побудованим на винятках, буває непросто. І коду стає більше, ніж з простими винятками.

### Коли використовувати Result Pattern

Використовуйте Result Pattern коли:

- Помилки є частиною нормального потоку виконання
- Потрібна висока продуктивність
- Хочете явно показати, що операція може не вдатися
- Потрібно ланцюжити операції з можливими помилками

НЕ використовуйте Result Pattern коли:

- Помилки справді виняткові (наприклад, OutOfMemoryException)
- Інтегруєтесь з API, які очікують винятки
- Команда не готова до додаткової складності

## Готові бібліотеки

Писати власну реалізацію не обов'язково, є готові бібліотеки:

[CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions) - популярна бібліотека з повним набором функціональних розширень

[LanguageExt](https://github.com/louthy/language-ext) - функціональна бібліотека з підтримкою Result та багато іншого

[ErrorOr](https://github.com/amantinband/error-or) - легка бібліотека для Result Pattern

## Висновок

`Result Pattern` найкраще працює там, де помилки є частиною нормального потоку виконання програми, як у прикладі з валідацією email. Платити за це доводиться додатковою складністю і довшим кодом, і окупається така ціна у великих проектах, де важливі надійність і зрозумілість коду.

Якщо хочете спробувати `Result Pattern`, почніть з простих сценаріїв і розширюйте використання, коли з'явиться досвід.

> Result Pattern - це інструмент, а не універсальне рішення. Використовуйте його там, де він справді допомагає, і поєднуйте з винятками там, де вони доречніші.
{: .prompt-info }
