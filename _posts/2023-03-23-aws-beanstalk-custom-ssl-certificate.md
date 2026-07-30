---
title: Використання TLS/SSL сертифікатів в .net додатку для AWS Beanstalk
author: Taras Kovalenko
date: 2023-03-23 12:00:00 +0200
categories: [.net, aws, security]
tags: [.net, ssl, tls, AWS, Beanstalk, Security]
image:
  path: /assets/img/posts/2023-03-23/aws-beanstalk.png
---

В [попередній](https://taraskovalenko.github.io/posts/configure-ssl-certificate-in-code/){:target="_blank"} статті ми розібралися як легко створити та додати власний SSL сертифікат до Azure App Service і використовувати його у нашому .net додатку. Сьогодні ми розберемось як виконати ідентичну задачу але будемо використовувати не Azure App Service а AWS Beanstalk.

## Що таке AWS Beanstalk?
---
AWS Elastic Beanstalk - це повністю керована платформа як послуга (PaaS), яка дозволяє розробникам розгортати та керувати веб-додатками та службами без необхідності керувати основною інфраструктурою.

За допомогою Beanstalk розробники можуть просто завантажити свій код, і Beanstalk автоматично виконає розгортання, масштабування та керування програмою. AWS Beanstalk також підтримує декілька мов програмування таких як .NET, Java, Python, Node.js ...

Beanstalk надає веб-консоль та інтерфейс командного рядка (CLI) для керування програмами, перегляду журналів і моніторингу продуктивності. Він також інтегрується з іншими службами AWS, такими як Amazon RDS, Amazon SNS і Amazon CloudWatch, щоб забезпечити додаткові функції та гнучкість.

В загальному AWS Beanstalk так само як і Azure App Service є зручним рішення для розробників, які хочуть зосередитися на створенні своїх програм, не турбуючись про базову інфраструктуру.

Але все ж таки як на мене, Azure App Service більше гнучкий та простий у використанні. (Можливо тому, що з Azure більше досвіду роботи 😉 )

## Як додати всласний SSL сертифікат для AWS Beanstalk?
---
Перш за все, тут не так все просто як з Azure App Service.
Для того щоб використовувати сертифікат нам потрібно встановити його, але в AWS Beanstalk не має можливості це зробити. Ми можемо додати його до артефактів і з допомогою CLI встановити його на екземпляр нашого Beanstalk додатку, але скорше за все він буде видалений при наступному розгортанні або оновленні. Також, якщо ми використовуємо декілька екземплярів та балансир навантаження, він не буде доступний поширений між ними.

Звісно ми можемо використовувати [AWS Private CA](https://aws.amazon.com/private-ca/){:target="_blank"}, але як на мене 400 доларів за місяць для приватного центу сертифікації це трішки за дорого. Особливо якщо ви розробляєте маленький додаток або MVP.

І так ми хочемо зробити це з мінімальними затратами і будемо використовувати власний SSL сертифікат створений із за допомогою OpenSSL.

> Як створити сертифікат ви можете прочитати у моїй попередній статті [Використання TLS/SSL сертифікатів в .net додатку для Azure App Service](/posts/configure-ssl-certificate-in-code/)
{: .prompt-info }

Ми маємо власний публічний та привітний сертифікат а також він захищений паролем. Для того щоб додати його до AWS ми будемо використовувати AWS Secrets Manager.
AWS Secrets Manager - це теж саме що і Azure Key Vault (про якй ми говорили в [попередній статті](https://taraskovalenko.github.io/posts/azure-key-vault/){:target="_blank"}) такий самий простий механізм збереження секретних даний і також дуже дешевий.

Ідея полягає в тому, що ми збережемо публічний та приватний сертифікат а також пароль до сертифікату в Secrets Manager, після цього ми зможемо отримати ці дану у нашому додатку та базуючись на них отримати готовий сертифікат.

Для початку відкрийте приватний та публічний (pem) сертифікати в текстовому редакторі та скопіюйте всю інформацію що в них знаходиться.
Наступний кроком перейдіть на AWS Console виберіть потрібний вам регіон та відкрийте AWS Secret Manager та натисніть "Store a new secret".

Вам буде запропоновано обрати тип даних які ви хочете зберегти, у нашому випадку це буде "Other type of secret". Перейдіть на вкладку "Plaintext" і вставте інформацію публічного ключа, це повинно виглядати наступним чином:

![aws-secret-0](/assets/img/posts/2023-03-23/aws-secret.png){: width="640" height="480"}

Натисніть далі, введіть імя та збережіть новий запис.
Теж саме потрібно зробити для приватного сертифікату і пароля.
В результаті у вас буде створено 3 записи в Secrets Manager.

## Зчитування та валідація сертифікату із Secrets Manager
---
Для початку нам потрібно встановити декілька NuGet пакетів:
```bash
dotnet add package AWSSDK.SecretsManager
dotnet add package Portable.BouncyCastle
```
AWSSDK.SecretsManager - бібліотека для роботи з AWS Secrets Manager.
Portable.BouncyCastle - надасть змогу прочитати PEM файл, перетворивши його на `X509Certificate2`, який ми можемо зможемо використати у нас в додатку.

> Щоб локально використовувати AWS ресурси вам потрібно налаштувати [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/cli-chap-getting-started.html){:target="_blank"}
{: .prompt-info }

І так давайте розглянемо як ми можемо отримати дані із AWS Secrets Manager.
Для зручності ми створимо окремий record який буде відповідати за отримання даних:

```cs
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

public interface ISecretsManagerClient
{
    Task<string> GetValueFromSecretManagerAsync(string secretName);
}

public record SecretsManagerClient : ISecretsManagerClient
{
    private const string VersionStage = "AWSCURRENT";
    private readonly IAmazonSecretsManager _secretsManager;

    public SecretsManagerClient(IAmazonSecretsManager secretsManager) =>
        _secretsManager = secretsManager;

    public SecretsManagerClient(string region) =>
        _secretsManager ??= new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

    public async Task<string> GetValueFromSecretManagerAsync(string secretName)
    {
        var request = new GetSecretValueRequest
        {
            SecretId = secretName,
            VersionStage = VersionStage
        };

        var response = await _secretsManager.GetSecretValueAsync(request);
        return response.SecretString;
    }
}
```

VersionStage - константа із значенням "AWSCURRENT" потрібна для отримання останньої версії наших даних. `GetSecretValueAsync` - стандартний метод із AWSSDK.SecretsManager який отримує дані із AWS Secrets Manager.

Наступним кроком потрібно створити сервіс для завантаження PEM даних, перевірки на коректність та генерації RSA сертифікату.

```cs
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

public interface ICertificateLoader
{
    Task<X509Certificate2> LoadCertificateAsync();
}

public class CertificateLoader : ICertificateLoader
{
    private readonly ISecretsManagerClient _secretsManager;

    public CertificateLoader(ISecretsManagerClient secretsManager)
    {
        _secretsManager = secretsManager;
    }

    public async Task<X509Certificate2> LoadCertificateAsync()
    {
        var password = await _secretsManager.GetValueFromSecretManagerAsync("PasswordSecretName");
        var pubicPemData = await _secretsManager.GetValueFromSecretManagerAsync("PublicSecretName");

        var pemData = Regex.Replace(Regex.Replace(pubicPemData, @"\s+", string.Empty), @"-+[^-]+-+", string.Empty);
        var pemBytes = Convert.FromBase64String(pemData);
        
        var x509Certificate2 = new X509Certificate2(pemBytes, password);

        var privatePemData = await _secretsManager.GetValueFromSecretManagerAsync("PrivateSecretName");
        var privateKey = DecodePrivateKey(privatePemData, password);

        var rsaParameters = DotNetUtilities.ToRSAParameters(privateKey.rsaPrivateKey);
        var rsa = RSA.Create();
        rsa.ImportParameters(rsaParameters);

        x509Certificate2 = x509Certificate2.CopyWithPrivateKey(rsa);
        return x509Certificate2;
    }

    private static (AsymmetricCipherKeyPair keyPair, RsaPrivateCrtKeyParameters rsaPrivateKey) DecodePrivateKey(
        string encryptedPrivateKey, string password)
    {
        TextReader textReader = new StringReader(encryptedPrivateKey);
        var pemReader = new PemReader(textReader, new PasswordFinder(password));
        var privateKeyObject = pemReader.ReadObject();
        var rsaPrivateKey = (RsaPrivateCrtKeyParameters) privateKeyObject;
        var rsaPublicKey = new RsaKeyParameters(false, rsaPrivateKey.Modulus, rsaPrivateKey.PublicExponent);
        var kp = new AsymmetricCipherKeyPair(rsaPublicKey, rsaPrivateKey);
        return (kp, rsaPrivateKey);
    }
}
```

Ми маємо `LoadCertificateAsync` метод - який відповідає безпосередньо за отримання та генерацію RSA сертифікату. Він робить наступну річ.
Спочатку отримуємо пароль та публічний сертифікат із Secret Manager, із за допомогою регулярного виразу видаляємо надлишкові дані, такі як коментарі що це публічний клю ат перетворюємо його в Base64 стрічку.

Створюємо екземпляр класу `X509Certificate2` в який передаємо наш публічний сертифікат та пароль.
Наступним кроком отримуємо приватний ключ і викликаємо `DecodePrivateKey` метод, який в свою чергу із за допомогою Portable.BouncyCastle бібліотеки
розшифрує та прочитає приватний сертифікат та поверне його нам у вигляді приватного(закритого) ключ RSA у форматі CRT (Chinese Remainder Theorem).

Якщо все успішно виконалось ми повертаємо копію нашого сертифікату `CopyWithPrivateKey` але вже даними про приватний сертифікат.

Ви також, можливо, помітили що PemReader отримує пароль як екземпляр класу `PasswordFinder`, це вимоги Portable.BouncyCastle, і для того щоб його реалізувати нам достатньо створити окремий клас який буде реалізовувати стандартний інтерфейс бібліотеки Portable.BouncyCastle під назвою `IPasswordFinder`.

```cs
using Org.BouncyCastle.OpenSsl;

internal sealed class PasswordFinder : IPasswordFinder
{
    private readonly string _password;

    public PasswordFinder(string password) => _password = password;

    public char[] GetPassword() => _password.ToCharArray();
}
```

## Висновок
---
І так ми розібрали як ми можемо зберігати сертифікат в AWS Secrets Manager та використовувати його в нашому додатку який розгорнутий на AWS Elastic Beanstalk. Також як використовувати Portable.BouncyCastle бібліотеку для розшифровування приватного сертифікату.
З одної боку це простіше ніж на Azure, тому що, не потрібно робити багато дій на порталі AWS але з іншого - потрібно писати код який буде отримувати окремо приватний та публічний (pem) файли та генерувати сертифікат базуючись на цих даних.
Все ж таки не має нічого не можливого, і завжди можна знайти рішення як вирішити ту чи іншу проблему.
