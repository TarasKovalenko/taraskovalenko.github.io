---
title: How to securely store and use sensitive data with Azure Key Vault
author: Taras Kovalenko
date: 2023-03-22 12:00:00.000000000 +02:00
categories:
- ".net"
- azure
- security
tags:
- ".net"
- ssl
- tls
- Azure
- Azure Key Vault
- Security
image:
  path: "/assets/img/posts/2023-03-22/azure-kv.png"
lang: en
locale: en_US
translation_key: azure-key-vault
permalink: "/en/posts/azure-key-vault/"
---

When developing almost any solution, we need to store some sensitive data. It can be both a connection string to the database with credentials about the user (login/password), and data about payment systems. In each application, this will be what is needed to perform the automation of certain tasks.
Usually, and this is dangerous and bad practice, this data is stored in application configuration files, and (unfortunately very often) this data is added to version control systems (git).
Why is this not correct? - First of all, the entire team of developers working on the application have a copy of this data, when one or another person is fired, you cannot be sure that your confidential data will not be used or distributed. Of course, you can replace the secret data with new ones and the previous ones will not be relevant, but this takes time and can also sometimes not be a very simple process if you have dependencies in other applications.
Also, if the source code of your application is stolen, all your confidential data, as well as the personal data of users who use it, will be at risk.

## How to store confidential data safely?
---
There are many mechanisms that ensure safe storage of secret data, it can be environment variables (Environment variables, if we are talking about .NET), certain encryption and decryption mechanisms, but in my opinion, one of the easiest options, and especially if you use Azure, is Azure Key Vault.
Azure Key Vault is a cloud service in which you can safely store passwords, keys, certificates and all necessary secret information.

In order to store data in Azure Key Vault, it must be created, for this, go to the [Azure](https://portal.azure.com/){:target="_blank"} portal, click "create a new resource" and select "Key Vault", fill in the required fields and click create.

![kv-create](/assets/img/posts/2023-03-22/kv-create.png){: width="1086" height="542"}

In a few seconds, your resource will be created and you can start using it.
To save secret data as key -> value, go to `Secrets` and click `Generate/Import`.

![kv-secret-1](/assets/img/posts/2023-03-22/kv-secret-1.png){: width="640" height="480"}
![kv-secret-2](/assets/img/posts/2023-03-22/kv-secret-2.png){: width="640" height="480"}

You also have the option to set when the data will be active for use and also the expiration date, but in our case we don't need that.

And yes, we have created a new Key Vault resource and added a new secret entry.

## How to get data from Key Vault in our app?
---
In order to start using data from Key Vault in a .NET application that will be deployed and available to users, we need to create a new application in `Azure Active Directory` that will have access to the service where our secret data is located.
To do this, go to the `Azure Active Directory -> App registrations` section and click create a new registration.
In the window that appears, it is enough to specify the name and press the "register" button.
![ad-app-1](/assets/img/posts/2023-03-22/ad-app-0.png){: width="640" height="480"}

After successfully creating a new Active Directory application, we need to add an SSL certificate, and with the help of the certificate we will check whether we can access the secret data that will be in our Azure Key Vault.> You can read how to create a certificate in my previous article [Using TLS/SSL certificates in a .NET application for Azure App Service](/en/posts/configure-ssl-certificate-in-code/)
{: .prompt-info }

![ad-app-1](/assets/img/posts/2023-03-22/ad-app-1.png){: width="640" height="480"}

The certificate is needed to confirm that the application that wants to receive data from Key Vault is really our application and it has an installed certificate (You can also see how to add a certificate to Azure App Service in the previous article).

And so we have a new registered Active Directory App, in order to get data from Key Vault we will need more information about this app.
To start we need `Thumbprint` of our certificate, you should see it right after adding it to App Active Directory, we also need `Application (client) ID` and `Directory (tenant) ID`, for this go to the "Overview" section.

![ad-app-2](/assets/img/posts/2023-03-22/ad-app-2.png){: width="640" height="480"}

To allow our Active Directory App to retrieve secret data from Azure Key Vault, we need to create a new access policy.

Go back to our created Azure Key Vault, go to the "Access policies" section and click "Create". In the new window, select `Key & Secret Management` from the drop-down list and click Next. In the next step of creating an access policy, select our Active Directory App. Go to the last creation step and click Create.

> Key & Secret Management - will provide access not only for reading but also for modification, deletion. If you don't need it then select only necessary permissions.
{: .prompt-warning }

![kv-access-0](/assets/img/posts/2023-03-22/kv-access-0.png){: width="640" height="480"}
![kv-access-1](/assets/img/posts/2023-03-22/kv-access-1.png){: width="640" height="480"}

We have configured the access policy and now we can write the code that will access our secret data.

## Writing code
---
To work with Azure Key Vault, we need to install two NuGet packages:

```bash
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

Azure.Identity - enables working with Active Directory, in our case authentication.
Azure.Extensions.AspNetCore.Configuration.Secrets - work directly with Azure Key Vault.

The next step is to modify the `appsettings.json` file and add information about Azure Key Vault and AD App.

```json
{
  "KeyVault": {
    "Vault": "taras-kv",
    "TenantId": "e4e08b73-e6c2-4c56-8cc0-b3813a030420",
    "ClientId": "36fc9800-c4b3-46a7-8f7e-b245a38ddc04",
    "Thumbprint": "67F592325E454FDE9B43FFEA5AFB6168"
  }
}
```

`appsettings.json` contains information such as the name of our Key Vault, `Application (client) ID` and `Directory (tenant) ID` our Active Directory App and `Thumbprint` the certificate we created and uploaded.

Now we need to write code that will use this data and upload to the secret data from Azure Key Vault.
To do this, we need to call the extension method `AddAzureKeyVault` for `IConfigurationBuilder`.

```cs
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

builder.Configuration.AddJsonFile($"appsettings.{env.EnvironmentName}.json");

var root = builder.Configuration;
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{root["KeyVault:Vault"]}.vault.azure.net/"),
    new ClientCertificateCredential(
        root["KeyVault:TenantId"],
        root["KeyVault:ClientId"],
        GetX509Certificate(root["KeyVault:Thumbprint"])
    )
);

var app = builder.Build();
app.Run();
```

> The implementation of method `GetX509Certificate` can be found in my previous article [Using TLS/SSL certificates in a .NET application for Azure App Service](/en/posts/configure-ssl-certificate-in-code/)
{: .prompt-info }

What does this code do? - first we add to the `appsettings.json` configuration a file that contains information about Key Vault and AD App. Form `Uri` for Key Vault and create an instance of the `ClientCertificateCredential` class to authenticate to Azure Active Directory using the specified certificate.

> The certificate must be installed on your PC in order for you to access the KV data.
{: .prompt-info }

This approach allows us to store confidential data in a secure Key Vault environment and only if you have an SSL certificate installed will you be able to read data from it.

But here comes the next problem, we need to give access to the certificate to all developers and this is again a security issue.
To avoid this problem, we can modify the code so that everyone who works with the application uses their own Azure account, and for this we just need to change `ClientCertificateCredential` to `AzureCliCredential`.

```cs
var root = builder.Configuration;
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{root["KeyVault:Vault"]}.vault.azure.net/"),
    new AzureCliCredential()
);
```

Now, if you try to run your application, the system will check if you are signed in to Azure using [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/authenticate-azure-cli){:target="_blank"}

```bash
az login
```

and accordingly access to Key Vault will be based on your Azure permissions.

## Conclusion
---
Saving secret configuration data is a very important process in software development, and the implementation of such mechanisms does not require much effort and money. Do not add your sensitive data to version control systems because you cannot be sure of their security.

We analyzed how to create an Azure Key Vault service, add an entry to it, and retrieve this entry from your application. Also, how to implement a mechanism in which it is not necessary to provide access to the SSL certificate to all project developers.

If you don't want to add to version control systems the information we added to `appsettings.json`, namely Key Vault name, TenantId, ClientId and Thumbprint, then you can use environment variables and get them directly from there, for example - `Environment.GetEnvironmentVariable("Vault")`.