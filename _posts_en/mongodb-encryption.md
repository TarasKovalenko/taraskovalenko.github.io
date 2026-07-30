---
title: Encryption in MongoDB - or why I switched to another database
author: Taras Kovalenko
date: 2025-03-10 09:00:00.000000000 +02:00
categories:
- ".net"
- C#
- security
tags:
- ".net"
- C#
- Security
- MongoDb
image:
  path: "/assets/img/posts/2025-03-10/mongocryptd.png"
lang: en
locale: en_US
translation_key: mongodb-encryption
permalink: "/en/posts/mongodb-encryption/"
---

_Article on How Modern Databases Require a Fossil Artifact to Protect Your Data_

## We protect data at any cost

Imagine: 2025, the era of AI, cloud technologies, microservices, containerization, serverless architecture. The technology world has reached unprecedented heights in simplifying development and deployment. And now for client-side encryption in MongoDB - where you have to run a separate exe :shit: file from the 2010s, which can only be found by downloading and installing Mongo Enterprise (or completing a quest to find this file on the Internet).

## Problem: I just need encryption

A seemingly simple requirement: to encrypt sensitive data in the MongoDB database.
In the technical documentation, it sounds attractive:

> "MongoDB supports client-side encryption (CSFLE), which allows sensitive fields to be encrypted before they are sent to the server."

But somewhere deep in the documentation, in small print, a real gem is hidden:

> "CSFLE requires mongocryptd, which is part of MongoDB Enterprise Server..."

In other words, to encrypt data in a cloud database in 2025, you will need a separate **exe** file.

## mongocryptd.exe in all its glory

Mongocryptd.exe is an encryption daemon that... sits on your client machine. Yes, to encrypt data in the cloud, you must run a local process on the machine where your application is running. Sounds like an architectural masterpiece, doesn't it?

Imagine a dialogue:

**Developer**: "We are deploying our application to Azure Web App."

**MongoDB**: "Great! What about encryption?"

**Programmer**: "Yes, we need to encrypt personal data."

**MongoDB**: "Outstanding! Just install mongocryptd.exe on your PaaS platform."

**Programmer**: "...but it's a managed platform. I can't just grab and install an exe file."

**MongoDB**: "Get creative! Maybe containerize an app? Or a virtual machine?"

**Programmer**: Sobbing quietly in the corner

## Code that will make you cry

Here's a small example of how to connect to MongoDB with encryption in .NET:

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
                // And here he is, our hero!
                // And don't forget to add the correct path depending on the OS!
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

But that's not all, you need to call the following method before creating `MongoClient`:

```cs
// And here it is! The icing on the cake! Awesome static extension method!
// Why do we need dependencies and DI when you can call a static method?
MongoClientSettings.Extensions.AddAutoEncryption();
```

Now imagine deploying that code to a cloud environment! Do you see this beautiful line construction with the path where our incredible `mongocryptd.exe` is located?

Think about it: in a world where we're talking about AI, cross-platform, and containerization, MongoDB requires you to specify the absolute path to the exe file on Windows.

### Setup Process: Five Steps to Madness

The most impressive thing is not only the presence of mongocryptd.exe, but also the encryption setup procedure itself:

1. **Step 1**: Install MongoDB Enterprise Server (because mongocryptd.exe is only available in this version)

2. **Step 2**: Write the path to mongocryptd.exe in the configuration

3. **Step 3**: Generate encryption keys and store in custom keyVault

4. **Step 4**: Set up an encryption scheme that specifies exactly which fields and how to encrypt

5. **Step 5**: And most importantly, call the magical static extension method `MongoClientSettings.Extensions.AddAutoEncryption()`, which must be called **before any other operations** to enable encryption. Forget about it, and your encryption simply won't work.

Forgot? Don't worry, MongoDB won't show you an understandable error. Your data will just sit quietly unencrypted until you realize something has gone wrong.
My favorite thing about working with mongocryptd is the file extension detection system. 

Here is the actual code from the MongoDB driver:

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

When you need a separate function to determine the file extension depending on the OS, this is the first signal that something has gone wrong in the architecture of your solution. And of course, let's not forget about the masterpiece code for starting the process:

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

## Deployment: pain and suffering

Want to deploy an app with mongocryptd.exe? Here are your "great" options:

1. **Azure Web App**: Forget it! You cannot run third party exe files unless you are using containers.
2. **Docker**: Yes, in theory you can, but be prepared for the pain of mixing a Windows container for mongocryptd.exe with a Linux container for your application.
3. **Kubernetes**: Of course you can, if you are willing to write custom scripts to start, monitor and restart the mongocryptd process.
4. **Virtual Machine**: Good old VM, like in 2005. The most reliable way, because you have full control. Just forget about all the advantages of PaaS and FaaS.

## Alternative options

MongoDB offers as many as two alternative options:

1. bypassAutoEncryption option: Disables encryption but leaves decryption. That is, you cannot write new encrypted data, but you can read existing ones. What a great alternative!
2. Using libmongocrypt: In theory, you can use the built-in library, but setting this option up is a separate quest that requires interviewing three elves and making a sacrifice on a full moon.

## Conclusion: Don't use this

After days of trying to set up encryption in MongoDB, here's my expert opinion: **don't do it**.

In 2025, there are much better alternatives:

**Azure Cosmos DB**: Native encryption that works without additional processes.
**Amazon DocumentDB**: Integrated encryption via AWS KMS.
**PostgreSQL with JSONB**: Combining the relational model with the document model, plus built-in encryption.

**MongoDB** has many advantages as a document-oriented database, but their implementation of encryption is like trying to integrate a fax machine into an iPhone. It might work, but is it worth it?

---

If you still decide to use encryption in MongoDB despite all the caveats, stock up on aspirin, patience, and planned refactoring time - when you inevitably decide to migrate to something less painful.