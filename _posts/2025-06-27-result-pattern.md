---
title: Result Pattern - елегантна альтернатива винятками та null-значенням
author: Taras Kovalenko
date: 2025-06-27 09:00:00 +0200
categories: [.net, C#, design patterns, software architecture]
tags: [.net, C#, patter, softwarearchitecture]
---

Управління помилками та відсутністю значень завжди було однією з найскладніших частин розробки програмного забезпечення. У світі .NET розробники традиційно покладалися на механізм винятків (exceptions) для обробки помилкових ситуацій та null-значення для позначення відсутності даних. Однак існує більш елегантний та функціональний підхід - `Result Pattern`, який дозволяє явно моделювати успішні та неуспішні результати операцій без використання винятків чи null-значень.

## Проблеми з традиційним підходом через винятки та null

Винятки в .NET, хоча й є потужним механізмом, мають кілька суттєвих недоліків, які можуть ускладнити розробку та підтримку коду.
Перш за все, винятки значно впливають на продуктивність програми, оскільки створення та викидання exception потребує додаткових ресурсів системи. Кожен виняток містить інформацію про стек викликів, що робить його створення відносно дорогою операцією.

Крім того, винятки ускладнюють читання та розуміння коду. Коли метод може викинути виняток, це не завжди очевидно з його сигнатури, що змушує розробників постійно документувати можливі винятки або шукати їх у коді. Це призводить до ситуацій, коли розробники забувають обробити певні типи помилок, що може спричинити непередбачувану поведінку програми.

Не менш проблематичними є null-значення. Коли метод повертає null, це може означати різні речі: відсутність даних, помилку в обробці, або просто неініціалізований стан. Розробникам доводиться постійно перевіряти значення на null, що робить код громіздким та схильним до `NullReferenceException`.

Ще одна проблема полягає в тому, що винятки порушують нормальний потік виконання програми. Коли виникає виняток, програма "стрибає" до найближчого обробника, минаючи весь проміжний код. Це може призвести до пропуску важливої логіки очищення ресурсів або інших критичних операцій.

## Що таке Result Pattern та як він спрощує роботу

`Result Pattern` має коріння у функціональному програмуванні, зокрема в мовах як Haskell (тип Either) та Rust (тип Result). Цей паттерн базується на принципі "зробити неможливі стани неможливими" та явному моделюванні успіху та неуспіху операцій.
В екосистемі .NET цей підхід набув популярності завдяки зростанню інтересу до функціонального програмування та необхідності створювати більш надійні та продуктивні системи.

## Коли винятки ускладнюють життя

Розглянемо типовий сценарій - валідацію email адреси:

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

Result Pattern базується на простій, але потужній ідеї: зробити помилки частиною типу даних. Замість того, щоб "кидати" помилку кудись у повітря (exception), ми "повертаємо" її як частину результату.

Ключові принципи Result Pattern:

- Явність над неявністю - метод чітко показує, що може повернути помилку
- Помилки як дані - помилка стає частиною типу повернення
- Композиція - легко ланцюжити операції, які можуть не вдатися
- Локальність - обробка помилок відбувається там, де викликається метод

## Магія методів розширення: Як Result стає справді потужним

Справжня сила Result Pattern розкривається через методи розширення. Вони дозволяють створювати елегантні пайплайни обробки даних, схожі на конвеєр на заводі - кожна операція додає щось до результату або зупиняє конвеєр у разі помилки.

Якщо щось йде не так на будь-якому етапі, конвеєр зупиняється і повідомляє про проблему.

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

Result Pattern чудово працює з LINQ, дозволяючи обробляти колекції результатів:

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

Result Pattern чудово працює з асинхронним кодом:

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

- Явність помилок
  Метод чітко показує, що він може повернути помилку. Це покращує читабельність коду та допомагає розробникам розуміти, що потрібно обробляти помилки.

- Продуктивність
  Відсутність винятків означає кращу продуктивність, особливо в сценаріях з частими помилками.

- Композиція
  Легко ланцюжити операції за допомогою методів розширення, створюючи елегантні пайплайни обробки даних.

- Тестування
  Простіше тестувати код, оскільки не потрібно ловити винятки - просто перевіряти властивості Result.

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

- Додаткова складність
  Result Pattern додає ще один рівень абстракції, що може ускладнити простий код.
  
- Сумісність з існуючим кодом
  Інтеграція з кодом, який використовує винятки, може бути складною.

- Розмір коду
  Код стає довшим порівняно з простим використанням винятків.

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

Замість написання власної реалізації, розгляньте готові бібліотеки:

[CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions) - популярна бібліотека з повним набором функціональних розширень

[LanguageExt](https://github.com/louthy/language-ext) - функціональна бібліотека з підтримкою Result та багато іншого

[ErrorOr](https://github.com/amantinband/error-or) - легка бібліотека для Result Pattern

## Висновок

`Result Pattern` - це потужний інструмент для створення надійного та зрозумілого коду. Він особливо корисний у сценаріях, де помилки є частиною нормального потоку виконання програми.

Ключові переваги:

- Явність помилок
- Краща продуктивність
- Елегантна композиція операцій
- Простіше тестування

Хоча `Result Pattern` додає деяку складність, він окупається у великих проектах, де важливі надійність та зрозумілість коду. Починайте з простих сценаріїв і поступово розширюйте використання по мірі накопичення досвіду.

> Result Pattern - це інструмент, а не срібна куля. Використовуйте його там, де він приносить найбільшу користь, і не соромтесь поєднувати з винятками там, де це доречно.
{: .prompt-info }
