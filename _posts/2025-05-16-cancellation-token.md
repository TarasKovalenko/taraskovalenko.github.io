---
title: CancellationToken в C# - використання, проблеми та кращі практики
author: Taras Kovalenko
date: 2025-05-16 09:00:00 +0200
categories: [.net, C#, Threading]
tags: [.net, C#, threading, CancellationToken]
---

## Що таке CancellationToken?

`CancellationToken` - це структура даних у C# .NET, яка дозволяє елегантно скасовувати асинхронні операції. Вона представляє собою механізм повідомлення, що передається між різними частинами коду для сигналізації про необхідність припинення виконання певної операції. `CancellationToken` сам по собі є лише об'єктом для перевірки стану скасування, але не може ініціювати скасування.

## Яку проблему вирішує CancellationToken?

У світі асинхронного програмування часто виникають ситуації, коли необхідно припинити виконання операції, що триває.
Без належного механізму скасування асинхронних операцій можуть виникати наступні проблеми:

- Витік ресурсів - асинхронні операції можуть тримати відкритими файли, мережеві з'єднання або інші системні ресурси.
- Зниження продуктивності - непотрібні операції продовжують виконуватись, витрачаючи процесорний час та пам'ять.
- Погіршення досвіду користувача - програма не реагує на запити користувача про зупинку довготривалих операцій.
- Складнощі з координацією - важко синхронізувати зупинку пов'язаних операцій.
- Неможливість обробки помилок - без механізму скасування складно обробляти ситуації, коли операція має бути перервана через помилку.

`CancellationToken` вирішує ці проблеми, надаючи стандартизований, кооперативний механізм скасування, який працює на всіх рівнях програми.

## Основні компоненти системи скасування

Система скасування в .NET складається з трьох ключових компонентів:

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

Кожен асинхронний метод повинен приймати `CancellationToken` як параметр. Це дозволяє спростити підтримку скасування операцій у всій програмі. Встановіть значення за замовчуванням `default`, щоб зробити параметр необов'язковим.

```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken = default)
{
    // Реалізація
}
```

Не створюйте методи без підтримки скасування, оскільки це ускладнить можливість їх скасування в майбутньому.

### Передавайте токен скасування в усі вкладені асинхронні операції

Передача токена скасування до всіх вкладених асинхронних операцій забезпечує коректне скасування всього ланцюжка операцій. Це дозволяє уникнути ситуацій, коли основна операція скасована, але вкладені операції продовжують виконуватися.

```csharp
public async Task ProcessDataAsync(CancellationToken cancellationToken = default)
{
    var data = await FetchDataAsync(cancellationToken);
    var processedData = await TransformDataAsync(data, cancellationToken);
    await SaveResultAsync(processedData, cancellationToken);
}
```

### Регулярно перевіряйте токен скасування в довготривалих операціях

В операціях з великими обсягами даних або циклами необхідно регулярно перевіряти токен скасування. Це дозволяє швидко реагувати на запит скасування і не витрачати ресурси на непотрібну роботу.

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

`CancellationTokenSource` реалізує інтерфейс `IDisposable` і має бути коректно звільнений. Використання контейнера `using` гарантує, що ресурси будуть звільнені, навіть якщо виникне виключення.

```csharp
using var cts = new CancellationTokenSource();
```

### Правильно обробляйте `OperationCanceledException`

Коли операція скасовується через `CancellationToken`, вона зазвичай генерує `OperationCanceledException`. Важливо коректно обробляти це виключення, розрізняючи очікуване скасування та інші помилки.

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

Замість ручного встановлення таймаутів з `Task.Delay` або `Task.WhenAny`, використовуйте вбудований механізм таймаутів у `CancellationTokenSource`. Це спрощує код і забезпечує правильне скасування операцій.

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

У деяких випадках краще використовувати перевірку `IsCancellationRequested` замість `ThrowIfCancellationRequested`. Це дозволяє реалізувати _м'яке_ скасування, при якому можна повернути проміжні результати або виконати додаткові дії перед завершенням.

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

Однак потрібно зауважити, що при такому підході статус задачі буде `RanToCompletion`, а не `Canceled`. Це може впливати на поведінку при використанні `Task.ContinueWith` або інших методів, які залежать від статусу задачі.

### Не захоплюйте токен скасування у замиканнях

При використанні лямбда-виразів або анонімних методів уникайте захоплення токена скасування. Замість цього передавайте його як параметр.

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

При роботі з `TaskCompletionSource`, зареєструйте токен скасування, щоб правильно скасовувати задачу.

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

Залежно від типу операції, встановлюйте відповідні таймаути:

```csharp
// Для запитів API
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// Для тривалих фонових операцій
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// Для коротких операцій
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
```

## Використання CancellationToken в ASP.NET Core

В ASP.NET Core кожен HTTP-запит отримує власний токен скасування, який автоматично скасовується, якщо клієнт закриває з'єднання. Це дозволяє елегантно припиняти обробку запитів, коли вони вже не потрібні.

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

`CancellationToken` особливо корисний при роботі з `HTTP`-запитами, коли користувач може вирішити скасувати операцію завантаження:

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

`CancellationToken` є потужним і гнучким механізмом для управління життєвим циклом асинхронних операцій у C# .NET. Він дозволяє елегантно скасовувати операції, коли вони більше не потрібні, уникаючи витоків ресурсів та покращуючи продуктивність програми.
Дотримуючись наведених кращих практик, ви зможете ефективно використовувати `CancellationToken` у своїх проєктах, створюючи надійні та ефективні асинхронні програми. Правильне використання механізму скасування є особливо важливим у серверних застосунках, де ефективне використання ресурсів має критичне значення для масштабованості та продуктивності.
Механізм скасування з використанням `CancellationToken` є рекомендованим підходом у сучасній розробці на C#, оскільки він:

- Забезпечує стандартизований механізм скасування
- Підтримується більшістю бібліотек та фреймворків .NET
- Інтегрується з іншими асинхронними API
- Дозволяє елегантно обробляти скасування на всіх рівнях програми
- Покращує загальну надійність та ефективність програми

Використовуючи `CancellationToken` у всіх асинхронних методах, ви створюєте код, який легше підтримувати, розширювати та тестувати.
