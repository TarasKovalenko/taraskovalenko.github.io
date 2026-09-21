---
title: CancellationToken в C# - використання, проблеми та кращі практики
author: Taras Kovalenko
date: 2025-05-16 09:00:00 +0200
categories: [.net, C#, Threading]
tags: [.net, C#, threading, CancellationToken]
---

## Що таке CancellationToken?

`CancellationToken` - це структура в C# .NET, через яку можна скасовувати асинхронні операції. Її передають між частинами коду як сигнал про те, що певну операцію треба зупинити. Сам `CancellationToken` скасування не ініціює: він лише дає змогу перевірити, чи його вже запросили.

## Яку проблему вирішує CancellationToken?

В асинхронному коді постійно трапляється потреба перервати операцію, яка вже виконується. Якщо механізму скасування немає, така операція далі тримає відкриті файли, мережеві з'єднання й інші системні ресурси та витрачає процесорний час і пам'ять на роботу, яка вже нікому не потрібна. Користувач просить зупинити довгу операцію, а програма не реагує.

Є й менш очевидні наслідки. Важко синхронно зупинити кілька пов'язаних операцій, і так само складно перервати операцію, коли її треба зупинити через помилку.

`CancellationToken` дає для цього стандартний кооперативний механізм скасування, який працює на всіх рівнях програми.

## Основні компоненти системи скасування

Система скасування в .NET складається з трьох компонентів:

`CancellationTokenSource` - клас, який створює токен і контролює сигнал скасування. Він має метод `Cancel()` (або асинхронний варіант `CancelAsync()`), який встановлює прапорець скасування.
`CancellationToken` - структура, яка передається в асинхронні методи. Вона має властивість `IsCancellationRequested`, яка показує, чи було запрошено скасування, та метод `ThrowIfCancellationRequested()`, який генерує виключення, якщо скасування було запрошено.
`OperationCanceledException` - виключення, яке виникає при скасуванні операції. Це стандартний спосіб сигналізації про те, що операція була скасована, а не завершилася з помилкою.

## Як використовувати CancellationToken?

```csharp
// Створення джерела токена скасування
using CancellationTokenSource cts = new CancellationTokenSource();
CancellationToken token = cts.Token;

try
{
    // Запуск асинхронної операції з передачею токена скасування
    Task task = LongRunningOperationAsync(token);

    // В іншому місці коду (наприклад, після натискання кнопки "Скасувати")
    await cts.CancelAsync();
    
    // Чекаємо завершення операції (навіть якщо скасовано)
    await task;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Операцію скасовано!");
}
```

Метод, що підтримує скасування, може виглядати так:

```csharp
async Task LongRunningOperationAsync(CancellationToken cancellationToken)
{
    for (int i = 0; i < 100; i++)
    {
        // Перевірка на скасування - викине OperationCanceledException при скасуванні
        cancellationToken.ThrowIfCancellationRequested();
        
        // Або альтернативна перевірка
        if (cancellationToken.IsCancellationRequested)
        {
            // Виконати очищення ресурсів якщо потрібно
            throw new OperationCanceledException(cancellationToken);
        }
        
        // Затримка, що підтримує скасування
        await Task.Delay(100, cancellationToken);
    }
}
```

### Скасування за таймаутом

`CancellationTokenSource` дозволяє автоматично скасовувати операції після певного проміжку часу:

```csharp
// Створення джерела токена з таймаутом 5 секунд
using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    // Операція буде скасована автоматично через 5 секунд
    await LongRunningOperationAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Операцію скасовано по таймауту!");
}
```

### Об'єднання токенів скасування

Можна об'єднувати кілька токенів скасування, щоб операція скасовувалась, якщо будь-який з токенів подає сигнал скасування:

```csharp
using CancellationTokenSource cts1 = new CancellationTokenSource();
using CancellationTokenSource cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));

// Створення джерела токена, яке буде скасовано, якщо будь-який з інших токенів буде скасовано
using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

try
{
    await LongRunningOperationAsync(linkedCts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Операцію скасовано!");
}
```

## Кращі практики використання CancellationToken

### Завжди додавайте параметр `CancellationToken` до асинхронних методів

Кожен асинхронний метод має приймати `CancellationToken` як параметр, тоді скасування легко протягнути через усю програму. Зі значенням за замовчуванням `default` параметр стає необов'язковим.

```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken = default)
{
    // Реалізація
}
```

Метод без такого параметра доведеться переробляти, щойно його знадобиться скасувати.

### Передавайте токен скасування в усі вкладені асинхронні операції

Якщо токен не дійшов до вкладених викликів, основна операція скасується, а вкладені працюватимуть далі. Тому передавайте `CancellationToken` у кожну вкладену асинхронну операцію, і скасується весь ланцюжок.

```csharp
public async Task ProcessDataAsync(CancellationToken cancellationToken = default)
{
    var data = await FetchDataAsync(cancellationToken);
    var processedData = await TransformDataAsync(data, cancellationToken);
    await SaveResultAsync(processedData, cancellationToken);
}
```

### Регулярно перевіряйте токен скасування в довготривалих операціях

Цикл або обробка великого обсягу даних можуть довго працювати без жодного асинхронного виклику, тож перевіряйте токен самі через регулярні проміжки. Так операція швидко помітить скасування і не витрачатиме ресурси на непотрібну роботу.

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

### Використовуйте контейнер using для `CancellationTokenSource`

`CancellationTokenSource` реалізує `IDisposable`, тому його треба звільняти. З `using` ресурси звільняться навіть тоді, коли виникне виключення.

```csharp
using var cts = new CancellationTokenSource();
```

### Правильно обробляйте `OperationCanceledException`

Скасована через `CancellationToken` операція зазвичай генерує `OperationCanceledException`. Обробляйте його окремо від інших виключень: очікуване скасування і справжня помилка - різні ситуації.

```csharp
try
{
    await DoWorkAsync(token);
}
catch (OperationCanceledException ex) when (ex.CancellationToken == token)
{
    // Очікуване скасування
    logger.Information("Операцію скасовано, як очікувалося");
}
catch (Exception ex)
{
    // Інші виключення - це помилки, які потрібно обробити
    logger.Error(ex, "Виникла неочікувана помилка");
}
```

### Використовуйте скасування замість таймаутів

Не збирайте таймаути вручну з `Task.Delay` або `Task.WhenAny`: у `CancellationTokenSource` таймаут уже вбудований. Код стає простішим, і операція справді скасовується.

```csharp
// Правильно - з підтримкою скасування
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await DoWorkAsync(cts.Token);

// Неправильно - без підтримки скасування
var task = DoWorkAsync();
var completed = await Task.WhenAny(task, Task.Delay(5000));
if (completed != task)
{
    // Операція перевищила таймаут, але продовжує виконуватись у фоні!
}
```

### Розглядайте використання `IsCancellationRequested` для _м'якого_ скасування

Іноді замість `ThrowIfCancellationRequested` зручніше перевіряти `IsCancellationRequested`. Так виходить _м'яке_ скасування: метод може повернути проміжні результати або виконати додаткові дії перед завершенням.

```csharp
public async Task<IEnumerable<Result>> ProcessBatchAsync(IEnumerable<Data> items, CancellationToken cancellationToken = default)
{
    var results = new List<Result>();
    
    foreach (var item in items)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            // Повертаємо проміжні результати замість викидання виключення
            return results;
        }
        
        var result = await ProcessItemAsync(item, cancellationToken);
        results.Add(result);
    }
    
    return results;
}
```

Але врахуйте: при такому підході статус задачі буде `RanToCompletion`, а не `Canceled`. Від цього залежить поведінка `Task.ContinueWith` та інших методів, які дивляться на статус задачі.

### Не захоплюйте токен скасування у замиканнях

У лямбда-виразах і анонімних методах не захоплюйте токен, а передавайте `CancellationToken` як параметр.

```csharp
// Неправильно - токен захоплюється у замиканні
CancellationToken token = cts.Token;
Task.Run(() => 
{
    // Захоплений токен
    while (!token.IsCancellationRequested) 
    {
        // Робота
    }
});

// Правильно - токен передається як параметр
Task.Run(() => 
{
    // Робота

    // Передача токена як параметр
}, cts.Token);
```

### Використовуйте `TaskCompletionSource` з токеном скасування

Задача з `TaskCompletionSource` сама про токен нічого не знає. Зареєструйте на токені зворотний виклик, який її скасує:

```csharp
public Task<T> CreateCancellableTask<T>(CancellationToken cancellationToken)
{
    var tcs = new TaskCompletionSource<T>();
    
    // Реєструємо скасування
    cancellationToken.Register(() => 
        tcs.TrySetCanceled(cancellationToken),
        useSynchronizationContext: false
    );
    
    // Використовуємо tcs для встановлення результату чи помилки
    
    return tcs.Task;
}
```

### Встановлюйте розумні часові обмеження для скасування

Таймаут залежить від типу операції:

```csharp
// Для запитів API
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// Для тривалих фонових операцій
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// Для коротких операцій
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
```

## Використання CancellationToken в ASP.NET Core

В ASP.NET Core кожен HTTP-запит отримує власний токен скасування, який автоматично скасовується, якщо клієнт закриває з'єднання. Тож обробку запиту можна припинити, щойно вона стала непотрібною.

```csharp
// Контролер
[HttpGet]
public async Task<IActionResult> GetDataAsync(CancellationToken cancellationToken)
{
    // Токен буде скасовано, якщо користувач закриє з'єднання
    var data = await _dataService.GetDataAsync(cancellationToken);
    return Ok(data);
}

// Сервіс
public class DataService(HttpClient httpClient) : IDataService
{    
    public async Task<Data> GetDataAsync(CancellationToken cancellationToken)
    {
        // Передаємо токен у HttpClient
        var response = await httpClient.GetAsync("api/data", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<Data>(cancellationToken: cancellationToken);
    }
}
```

## Реальні приклади використання CancellationToken

### Скасування HTTP-запитів у HttpClient

З `HTTP`-запитами `CancellationToken` потрібен, коли користувач може скасувати завантаження:

```csharp
public async Task<string> GetWebContentAsync(string url, CancellationToken cancellationToken = default)
{
    using HttpClient client = new HttpClient();
    
    // Встановлюємо таймаут на запит
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken, timeoutCts.Token);
    
    try
    {
        // Використовуємо об'єднаний токен для запиту
        HttpResponseMessage response = await client.GetAsync(url, linkedCts.Token);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync(linkedCts.Token);
    }
    catch (OperationCanceledException ex)
    {
        if (timeoutCts.Token.IsCancellationRequested)
            throw new TimeoutException($"Запит до {url} перевищив таймаут", ex);
        
        // Інше скасування (наприклад, користувачем)
        throw;
    }
}
```

### Паралельна обробка даних з можливістю скасування

```csharp
public async Task ProcessFilesAsync(string[] filePaths, CancellationToken cancellationToken = default)
{
    // Створюємо список задач
    var tasks = new List<Task>();
    
    foreach (var filePath in filePaths)
    {
        // Перевіряємо скасування перед запуском нової задачі
        cancellationToken.ThrowIfCancellationRequested();
        
        tasks.Add(ProcessFileAsync(filePath, cancellationToken));
    }
    
    try
    {
        // Очікуємо завершення всіх задач із можливістю скасування
        await Task.WhenAll(tasks);
    }
    catch (OperationCanceledException)
    {
        // Логуємо скасування та пробуємо зберегти проміжні результати
        Console.WriteLine("Обробка файлів скасована.");
        
        // Тут можна зберегти проміжні результати
    }
}

private async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
{
    // Реалізація обробки файлу з періодичною перевіркою скасування
}
```

### Реалізація періодичних фонових задач з підтримкою скасування

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
                // Виконуємо періодичне завдання
                await PerformWorkAsync(cancellationToken);
                
                // Очікуємо до наступного циклу з можливістю скасування
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Очікуване скасування
                break;
            }
            catch (Exception ex)
            {
                // Логуємо помилку, але продовжуємо роботу
                Console.WriteLine($"Помилка фонової задачі: {ex.Message}");
                
                // Коротка пауза перед наступною спробою
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
        // Реалізація роботи з періодичною перевіркою скасування
    }
    
    public void Stop()
    {
        _cts.Cancel();
    }
    
    public async Task StopAndWaitAsync(TimeSpan timeout)
    {
        _cts.Cancel();
        
        // Очікуємо завершення задачі з таймаутом
        using var timeoutCts = new CancellationTokenSource(timeout);
        
        try
        {
            await _workerTask.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            Console.WriteLine("Не вдалося дочекатися завершення фонової задачі");
        }
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
```

## Важливі зауваження щодо використання CancellationToken

- Скасування є кооперативним. Операції не зупиняються автоматично - вони повинні періодично перевіряти токен скасування та реагувати на нього. Це означає, що код, який не перевіряє токен, не буде скасований.
- Скасування не означає негайне припинення. Після виклику `Cancel()`, операції можуть продовжувати виконуватися, доки не перевірять токен скасування. Це дозволяє операціям завершитися коректно.
- `CancellationTokenSource` споживає ресурси. Завжди використовуйте `using` або викликайте `Dispose()` після використання, щоб уникнути витоку ресурсів.
- Токен скасування слід передавати, а не створювати на кожному рівні. Створюйте `CancellationTokenSource` на найвищому рівні ієрархії викликів, а потім передавайте токен вниз по ланцюжку викликів.
- Скасування має відбуватися швидко. Методи не повинні виконувати трудомісткі операції після виявлення скасування. Вони повинні очистити ресурси та завершитися якомога швидше.

## Висновок

`CancellationToken` - стандартний спосіб скасування в C# .NET: його підтримує більшість бібліотек і фреймворків, і він інтегрується з іншими асинхронними API. Але механізм кооперативний. Він працює лише тоді, коли код передає токен далі й регулярно його перевіряє, а метод, який не приймає `CancellationToken`, ззовні вже не скасуєш.

На практиці вистачає кількох звичок: приймайте токен у кожному асинхронному методі, передавайте його у вкладені виклики, перевіряйте в довгих циклах і звільняйте `CancellationTokenSource`. Найбільше це дає серверним застосункам. Запит, який уже нікому не потрібен, перестає займати ресурси, і від цього напряму залежать масштабованість і продуктивність.
