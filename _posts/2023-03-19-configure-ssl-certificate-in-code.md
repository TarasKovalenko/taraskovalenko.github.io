---
title: Використання TLS/SSL сертифікатів в .net додатку для Azure App Service
author: Taras Kovalenko
date: 2023-03-19 12:00:00 +0200
categories: [.net, azure, tls/ssl, security]
tags: [.net, ssl, tls, Azure, Azure App Service, Security]
image:
  path: /assets/img/posts/2023-03-19/tls-ssl.png
---

З кожним днем все більше і більше компаній зазнають зламів та втрати/витоки персональних даних. В багатьох випадках це звичайний <a href="https://uk.wikipedia.org/wiki/%D0%A4%D1%96%D1%88%D0%B8%D0%BD%D0%B3" target="_blank">фішинг</a> та неуважність користувачів, але також є багато випадків коли компанії нехтують безпекою та не проводять тестування на кібератаки (<a href="https://uk.wikipedia.org/wiki/%D0%A2%D0%B5%D1%81%D1%82_%D0%BD%D0%B0_%D0%BF%D1%80%D0%BE%D0%BD%D0%B8%D0%BA%D0%BD%D0%B5%D0%BD%D0%BD%D1%8F" target="_blank">Penetration test</a>).

## Що таке TLS/SSL сертифікати?
---
Щоб зрозуміти для чого нам потрібні TLS/SSL сертифікати уявіть ситуацію, що ви робите онлайн замовлення та оплачуєте його в інтернеті. Здебільшого вас просять ввести номер банківської картки та додаткову інформацію про неї, таку як CVV код та дату до якої карта дійсна. 
Як тільки ви це зробили і натиснули кнопку оплатити, ваші дані будуть надіслані на сервер, де буде проходити перевірка справжність картки і надсилання квитанції про купівлю товару. Якщо все пройшло успішно, банк знімає з картки гроші. 

Зазвичай дані з веб-сайту до сервера передаються у відкритому вигляді, і якщо під час запиту передачі даних на сервер шахраї зможуть перехопити інформацію ви про це не зможете дізнатися. Скоріше за все ви дізнаєтесь що дані вашої карти було викрадено тільки коли з карти будуть списані кошти.

Так от, щоб уникнути таких ситуацій потрібно зашифрувати дані які будуть відправлятися від клієнта до сервера. Існує безліч механізмів шифрування даних і одним із самим надійним на даний момент є SSL сертифікат.

**Сертифікат TLS/SSL** — це цифровий об'єкт, який дозволяє системам перевіряти особу та встановлювати зашифроване мережеве з’єднання з іншою системою за допомогою протоколу Transport Layer Security/Secure Sockets Layer (TLS/SSL).

У роботі SSL-сертифіката бере участь два типи шифрування: 
- **Симетричне** - це коли один ключ зашифровує та розшифровує повідомлення. 
- **Асиметричне** — коли є два різних ключі: публічний і приватний. Публічний лише зашифровує повідомлення, його бачить кожний веб-переглядач. Приватний лише розшифровує та зберігається в таємниці на сервері.

Простими словами, всі ваші дані будуть зашифровані і навіть якщо шахраї перехоплять вашу персональну інформацію, їм доведеться витратити дуже багато часу щоб її розшифрувати.

## Як згенерувати сертифікат?
---
Ми розібралися що таке сертифікат і для чого він нам потрібний. Давайте розглянемо як ми можемо згенерувати власний сертифікат.
Існує велика кількість продуктів із за допомогою яких ви можете отримати власний сертифікат, для прикладу <a href="https://letsencrypt.org/" target="_balnk">LetsEncrypt</a>, <a href="https://www.cloudflare.com/" target="_balnk">Cloudflare</a>, <a href="https://www.openssl.org/" target="_balnk">OpenSSL</a>, в даній статті я покажу як створити сертифікат із за допомогою OpenSSL а також як це зробити із за допомогою .net/C#.

Отже для того щоб створити сертифікат із за допомогою коду, все що потрібно це використати стандартну бібліотеку <a href="https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates?view=net-7.0" target="_blank">System.Security.Cryptography.X509Certificates</a>.

```cs
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using var algorithm = RSA.Create(keySizeInBits: 2048);

var subject = new X500DistinguishedName("CN=TKovalenko Encryption Certificate");
var request = new CertificateRequest(subject, algorithm, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true));

var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(2));

var password = "942Rq7MIp!nn";
File.WriteAllBytes("tkovalenko-encryption-certificate.pfx", certificate.Export(X509ContentType.Pfx, password));
```
> Не використовуйте даний приклад коду для реальних production-ready додатків.
{: .prompt-warning }

Давайте розберемо, що даний приклад коду робить.
Спочатку ми сказали що хочемо використовувати RSA асиметричний алгоритм кодування та розмір колюча в 2048 біт. Після цього вказали мінімальну інформацію про сертифікат та вказали який алгоритм хешування використовувати для підпису сертифіката та/або запит на сертифікат і вказали режим заповнення та параметри для операцій створення або перевірки підпису RSA.
Також ми вказали термін дії сертифікату і пароль.
Якщо ви запустите даний приклад коду, то як результат виконання даної програми ви отримаєте _tkovalenko-encryption-certificate.pfx_ сертифікат який захищений паролем.

Щоб створити такий сертифікат із за допомогою OpenSSL, вам буде потрібно провести виконати наступні команди в командній стрічці.
> Перед виконанням команд переконайтеся, що OpenSSL встановлений на ваш ПК.
{: .prompt-info }

Спочатку нам потрібно створити публічний та приватний ключі:
```bash
openssl req -newkey rsa:2048 -nodes -keyout tkovalenko-encryption-certificate-key.pem -x509 -days 530 -out tkovalenko-encryption-certificate.pem
```
Що ця команда означає? Ми сказали openssl створити новий ключ який буде використовувати RSA асиметричний алгоритм кодування в тому ж самому розмірі 2048 біт.
_-tkeyout_ - tkovalenko-encryption-certificate-key.pem імя нашого приватного ключа, _-x509_ - тип сертифіката, _-days_ - термін дії сертифікату в днях 530 (2 роки),
_-out_ - tkovalenko-encryption-certificate.pem ім'я публічного колюча.
Після цього OpenSSL запропонує ввести дані про сертифікат, той же CN= і т.д. і по завершенню ми отримаємо публічний та приватний ключ.

Щоб згенерувати **pfx** файл нам потрібно виконати наступну команду:
```bash
openssl pkcs12 -inkey tkovalenko-encryption-certificate-key.pem -in tkovalenko-encryption-certificate.pem -export -out tkovalenko-encryption-certificate.pfx
```
І так, ми сказали openssl щоб використовувати _PKCS12_ формат для зберігання багатьох об'єктів криптографії в одному файлі і також вказали назви нашого публічно та приватного ключів.
_-export -out_ - tkovalenko-encryption-certificate.pfx імя нашого фінального pfx сертифікату.
Після виконання даної команди, OpenSSL запропонує ввести пароль для захисту сертифікату.

Отже як результат ми отримаємо _tkovalenko-encryption-certificate.pfx_ сертифікат який захищений паролем, так само як ми це зробили із за допомогою .net/C#.

## Використання TLS/SSL сертифікатів в .net додатку для Azure App Service
---
І так, ми знаємо що таке TLS/SSL, також вміємо згенерувати власний сертифікат, прийшов час додати його до нашого додатку який знаходиться на Azure App Service.

Для початку потрібно перейти на портал <a href="https://portal.azure.com/" targt="_blank">Azure</a>, вибрати потрібний App Service, після цього потрібно перейти в розділ сертифікати та завантажити ваш pfx. Натисніть Validate, якщо пароль та сертифікат коректний ви можете додати його до Azure App Service.

![azure-portal](/assets/img/posts/2023-03-19/azure-portal.png){: width="1086" height="542"}
> Azure не дозволить завантажити сертифікат який не захищений паролем.
{: .prompt-warning }

Сертифікат завантажений на наш App Service але поки що ми не можемо його використовувати так як не маємо доступу до нього з коду нашого майбутнього додатка.

Щоб це виправити потрібно перейти в розділ конфігурації та додати новий app setting, `WEBSITE_LOAD_CERTIFICATES` із значенням `*` і перезавантажити App Service.

![azure-app-setting](/assets/img/posts/2023-03-19/azure-app-setting.png){: width="1086" height="542"}

### Зчитування сертифіката із .net додатку

Ми згенерували та завантажили сертифікат на Azure App Service, отже саме час зчитати його з нашої програми і почати використовувати для захисту даних:

```cs
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

public static X509Certificate2 GetX509Certificate(string thumbprint)
{
  var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
  try
  {
    store.Open(OpenFlags.ReadOnly);
    var certificateCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

    if (certificateCollection.Count == 0)
    {
      throw new Exception("Certificate is not installed");
    }

    return certificateCollection.First();
  }
  finally
  {
    store.Close();
  }
}
```

Метод _GetX509Certificate_ - очікує на вхідний параметр _thumbprint_ (це унікальний ідентифікатор для сертифікату) і шукає по ньому сертифікат для поточного користувача.

Щоб отримати _thumbprint_ нам потрібно перейти назад на портал <a href="https://portal.azure.com/" targt="_blank">Azure</a> вибрати наш App Service і перейти в розділ сертифікатів, та переглянути інформацію про сертифікат.

![thumbprint](/assets/img/posts/2023-03-19/thumbprint.png){: width="480" height="640"}

## Висновок
---
Ми розібралися що таке TLS/SSL сертифікати для чого вони потрібні, як згенерувати власний сертифікат та завантажити його на Azure App Service і отримати до нього доступ з нашого додатку для подальшого використання.
