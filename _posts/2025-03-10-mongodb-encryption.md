---
title: Шифрування в MongoDB - aбо чому я перейшов на іншу базу даних
author: Taras Kovalenko
date: 2025-03-10 09:00:00 +0200
categories: [.net, C#, security]
tags: [.net, C#, Security, MongoDb]
image:
  path: /assets/img/posts/2025-03-10/mongocryptd.png
---

_Стаття про те, як модерна база даних вимагає викопного артефакту для захисту ваших даних_

## Захищаємо дані за будь-яку ціну

Уявіть собі: 2025 рік, ера AI, хмарних технологій, мікросервісів, контейнеризації, serverless архітектури. Технологічний світ досяг небачених висот у спрощенні розробки та розгортання. А тепер познайомтеся з шифруванням на стороні клієнта в MongoDB — де вам доведеться запускати окремий exe :shit: файл з 2010-х років, який можна знайти тільки якщо завантажити та встановивши Mongo Enterprise (або проходячи квест з пошуку цього файлу на просторах інтернету).

## Проблема: мені потрібне просто шифрування

Здавалося б, проста вимога: шифрувати чутливі дані в базі MongoDB.
У технічній документації це звучить привабливо:

> "MongoDB підтримує шифрування на стороні клієнта (CSFLE), що дозволяє шифрувати конфіденційні поля перед їх відправленням на сервер."

Але десь глибоко в документації, дрібним шрифтом, заховано справжню перлину:

> "Для CSFLE потрібен mongocryptd, який є частиною MongoDB Enterprise Server..."

Іншими словами, щоб шифрувати дані в хмарній базі даних у 2025 році, вам знадобиться окремий **exe** файл.

## mongocryptd.exe в усій красі

Mongocryptd.exe — це демон шифрування, який... сидить на вашому клієнтському комп'ютері. Так-так, для шифрування даних у хмарі ви повинні запустити локальний процес на машині, де працює ваш додаток. Звучить як архітектурний шедевр, чи не так?

Уявіть діалог:

**Програміст**: "Ми розгортаємо нашу програму в Azure Web App."

**MongoDB**: "Чудово! А як щодо шифрування?"

**Програміст**: "Так, нам потрібно шифрувати персональні дані."

**MongoDB**: "Неперевершено! Просто встановіть mongocryptd.exe на вашу PaaS платформу."

**Програміст**: "...але це керована платформа. Я не можу просто взяти і встановити exe файл."

**MongoDB**: "Творчо підійдіть до питання! Може, контейнеризуєте додаток? Або віртуальну машину?"

**Програміст**: ридає тихо в кутку

## Код, який змусить вас плакати

Ось невеликий приклад, як встановити з'єднання з MongoDB із шифруванням у .NET:

```cs
private const string LocalMasterKey =
            "Mng0NCt4ZHVUYUJCa1kxNkVyNUR1QURhZ2h2UzR2d2RrZzh0cFBwM3R6NmdWMDFBMUN3YkQ5aXRRMkhGRGdQV09wOGVNYUMxT2k3NjZKelhaQmRCZGJkTXVyZG9uSjFk";

public MongoClient CreateMongoClient()
{
    var localMasterKey = Convert.FromBase64String(LocalMasterKey);

    var kmsProviders = new Dictionary<string, IReadOnlyDictionary<string, object>>();
    var localKey = new Dictionary<string, object> { { "key", localMasterKey } };
    kmsProviders.Add("local", localKey);

    var keyVaultNamespace = CollectionNamespace.FromFullName("encryption.__keyVault");
    var autoEncryptionOptions = new AutoEncryptionOptions(
        keyVaultNamespace,
        kmsProviders,
        extraOptions: new Optional<IReadOnlyDictionary<string, object>>(
            new Dictionary<string, object>
            {
                // А ось і він, наш герой!
                // І не забудьте додати правильний шлях залежно від ОС!
                { "mongocryptdSpawnPath", "C:\\Program Files\\MongoDB\\Server\\8.0\\bin" },
            }
        )
    );

    var mongoClientSettings = new MongoClientSettings
    {
        AutoEncryptionOptions = autoEncryptionOptions,
    };

    return new MongoClient(mongoClientSettings);
}
```

Але це ще не все, вам потрібно перед тим як створити `MongoClient` викликати наступний метод:

```cs
// І ось воно! Вишенька на торті! Чудовий статичний метод розширення!
// Нащо нам залежності і DI, коли можна викликати статичний метод?
MongoClientSettings.Extensions.AddAutoEncryption();
```

А тепер уявіть, що цей код потрібно розгорнути в хмарному середовищі! Чи бачите цю прекрасну рядкову конструкцію з шляхом де знаходиться наш неймовірний `mongocryptd.exe`?

Вдумайтеся: у світі, де ми обговорюємо АІ, кросплатформність, та контейнеризацію, MongoDB вимагає вказати абсолютний шлях до exe файлу у Windows.

### Процес налаштування: п'ять кроків до божевілля

Найбільше вражає не лише наявність mongocryptd.exe, але й сам порядок налаштування шифрування:

1. **Крок 1**: Встановіть MongoDB Enterprise Server (бо mongocryptd.exe доступний лише в цій версії)

2. **Крок 2**: Пропишіть шлях до mongocryptd.exe в конфігурації

3. **Крок 3**: Створіть ключі шифрування та збережіть у спеціальному keyVault

4. **Крок 4**: Налаштуйте схему шифрування, в якій точно вказано які поля та як шифрувати

5. **Крок 5**: І найголовніше — викличте чарівний статичний метод розширення `MongoClientSettings.Extensions.AddAutoEncryption()`, який треба викликати **перед будь-якими іншими операціями** для активації шифрування. Забудьте про нього — і ваше шифрування просто не працюватиме.

Забули? Не турбуйтеся, MongoDB не покаже вам зрозумілу помилку. Ваші дані просто спокійно зберігатимуться незашифрованими, аж поки ви не зрозумієте, що щось пішло не так.
Мій улюблений момент в роботі з mongocryptd — це система визначення розширення файлу. 

Ось справжній код з драйвера MongoDB:

```csharp
string GetMongocryptdExtension()
{
    var currentOperatingSystem = OperatingSystemHelper.CurrentOperatingSystem;
    switch (currentOperatingSystem)
    {
        case OperatingSystemPlatform.Windows:
            return ".exe";
        case OperatingSystemPlatform.Linux:
        case OperatingSystemPlatform.MacOS:
        default:
            return "";
    }
}
```

Коли вам потрібна окрема функція для визначення розширення файлу залежно від ОС, це перший сигнал, що щось пішло не так в архітектурі вашого рішення. І звичайно ж, давайте не забуваємо про шедевральний код запуску процесу:

```cs
private static void StartProcess(string path, string args)
{
    try
    {
        using (var process = new Process())
        {
            process.StartInfo.Arguments = args;
            process.StartInfo.FileName = path;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            if (!process.Start())
            {
                // skip it. This case can happen if no new process resource is started
                // (for example, if an existing process is reused)
            }
        }
    }
    catch (Exception ex)
    {
        throw new MongoClientException("Exception starting mongocryptd process. Is mongocryptd on the system path?", ex);
    }
}
```

## Розгортання: біль і страждання

Хочете розгорнути додаток з mongocryptd.exe? Ось ваші "прекрасні" варіанти:

1. **Azure Web App**: Забудьте! Ви не можете запускати сторонні exe-файли, якщо не використовуєте контейнери.
2. **Docker**: Так, теоретично можна, але готуйтеся до болю при змішуванні Windows-контейнера для mongocryptd.exe з Linux-контейнером для вашого додатку.
3. **Kubernetes**: Звичайно, можна, якщо ви готові писати спеціальні сценарії для запуску, моніторингу та перезапуску mongocryptd процесу.
4. **Віртуальна машина**: Стара добра VM, як у 2005-му. Найнадійніший спосіб, бо у вас повний контроль. Просто забудьте про всі переваги PaaS і FaaS.

## Альтернативні варіанти

MongoDB пропонує аж два альтернативних варіанти:

1. Параметр bypassAutoEncryption: Відключає шифрування, але залишає дешифрування. Тобто ви не можете записувати нові шифровані дані, але можете читати існуючі. Яка чудова альтернатива!
2. Використання libmongocrypt: Теоретично, можна використовувати вбудовану бібліотеку, але налаштування цього варіанту — окремий квест, який вимагає співбесіди з трьома ельфами та жертвопринесення у повний місяць.

## Висновок: не використовуйте це

Після днів спроб налаштувати шифрування в MongoDB, ось мій експертний висновок: **не робіть цього**.

У 2025 році існують значно кращі альтернативи:

**Azure Cosmos DB**: Нативне шифрування, яке працює без додаткових процесів.
**Amazon DocumentDB**: Інтегроване шифрування через AWS KMS.
**PostgreSQL з JSONB**: Поєднання реляційної моделі з документарною, плюс вбудоване шифрування.

**MongoDB** має безліч переваг як документоорієнтована база даних, але їхня реалізація шифрування — це наче намагатися інтегрувати факс-машину в iPhone. Можливо, це працює, але чи варто воно того?

---

Якщо ви, незважаючи на всі застереження, все ж вирішили використовувати шифрування в MongoDB, запасіться аспірином, терпінням та запланованим часом на рефакторинг — коли ви неминуче вирішите мігрувати на щось менш болюче.
