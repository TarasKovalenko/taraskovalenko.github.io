---
title: Using TLS/SSL certificates in a .NET application for AWS Beanstalk
author: Taras Kovalenko
date: 2023-03-23 12:00:00.000000000 +02:00
categories:
- ".net"
- aws
- security
tags:
- ".net"
- ssl
- tls
- AWS
- Beanstalk
- Security
image:
  path: "/assets/img/posts/2023-03-23/aws-beanstalk.png"
lang: en
locale: en_US
translation_key: aws-beanstalk-custom-ssl-certificate
permalink: "/en/posts/aws-beanstalk-custom-ssl-certificate/"
---

In the [previous](https://taraskovalenko.github.io/en/posts/configure-ssl-certificate-in-code/){:target="_blank"} article, we explained how to easily create and add your own SSL certificate to Azure App Service and use it in our .NET application. Today we will figure out how to perform an identical task, but we will use AWS Beanstalk instead of Azure App Service.

## What is AWS Beanstalk?
---
AWS Elastic Beanstalk is a fully managed platform as a service (PaaS) that allows developers to deploy and manage web applications and services without having to manage the underlying infrastructure.

With Beanstalk, developers can simply upload their code and Beanstalk will automatically deploy, scale, and manage the application. AWS Beanstalk also supports several programming languages such as .NET, Java, Python, Node.js ...

Beanstalk provides a web console and command line interface (CLI) for managing applications, viewing logs, and monitoring performance. It also integrates with other AWS services such as Amazon RDS, Amazon SNS, and Amazon CloudWatch to provide additional features and flexibility.

In general, AWS Beanstalk, as well as Azure App Service, is a convenient solution for developers who want to focus on creating their applications without worrying about the underlying infrastructure.

But still, in my opinion, Azure App Service is more flexible and easy to use. (Maybe because I have more work experience with Azure 😉 )

## How to add your own SSL certificate for AWS Beanstalk?
---
First of all, everything is not as simple here as with Azure App Service.
In order to use the certificate, we need to install it, but in AWS Beanstalk there is no way to do this. We can add it to the artifacts and use the CLI to install it on an instance of our Beanstalk application, but it will most likely be removed in the next deployment or upgrade. Also, if we use multiple instances and a load balancer, it will not be available spread between them.

Of course we can use [AWS Private CA](https://aws.amazon.com/private-ca/){:target="_blank"}, but $400 per month for a private CA is a bit expensive for me. Especially if you are developing a small app or MVP.

And so we want to do it with minimal costs and will use our own SSL certificate created with the help of OpenSSL.

> You can read how to create a certificate in my previous article [Using TLS/SSL certificates in a .NET application for Azure App Service](/en/posts/configure-ssl-certificate-in-code/)
{: .prompt-info }

We have our own public and friendly certificate and it is also password protected. In order to add it to AWS, we will use AWS Secrets Manager.
AWS Secrets Manager is also the same as Azure Key Vault (which we talked about in [the previous article](https://taraskovalenko.github.io/en/posts/azure-key-vault/){:target="_blank"}) the same simple mechanism for storing secret data and is also very cheap.

The idea is that we will store the public and private certificate as well as the certificate password in the Secrets Manager, after that we can get this data in our application and get a ready certificate based on it.First, open the private and public (pem) certificates in a text editor and copy all the information contained in them.
The next step is to go to the AWS Console, select the region you want and open the AWS Secret Manager and click "Store a new secret".

You will be asked to choose the type of data you want to save, in our case it will be "Other type of secret". Go to the "Plaintext" tab and paste the public key information, it should look like this:

![aws-secret-0](/assets/img/posts/2023-03-23/aws-secret.png){: width="640" height="480"}

Click next, enter a name and save the new entry.
The same should be done for the private certificate and password.
As a result, you will have 3 entries in Secrets Manager.

## Reading and validating the certificate with Secrets Manager
---
To get started, we need to install a few NuGet packages:

```bash
dotnet add package AWSSDK.SecretsManager
dotnet add package Portable.BouncyCastle
```

AWSSDK.SecretsManager - a library for working with AWS Secrets Manager.
Portable.BouncyCastle - will make it possible to read a PEM file, converting it to `X509Certificate2`, which we can use in our application.

> To use AWS resources locally you need to configure [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/cli-chap-getting-started.html){:target="_blank"}
{: .prompt-info }

And so let's consider how we can get data from AWS Secrets Manager.
For convenience, we will create a separate record that will be responsible for receiving data:

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

VersionStage - a constant with the value "AWSCURRENT" is required to get the latest version of our data. `GetSecretValueAsync` is a standard method from AWSSDK.SecretsManager that receives data from AWS Secrets Manager.

The next step is to create a service for downloading PEM data, checking for correctness and generating an RSA certificate.

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

We have a `LoadCertificateAsync` method - which is directly responsible for obtaining and generating an RSA certificate. He does the following thing.
First, we get the password and public certificate from the Secret Manager, and with the help of a regular expression, we remove redundant data, such as comments that this is a public key, and convert it into a Base64 string.

We create an instance of the `X509Certificate2` class into which we transfer our public certificate and password.
The next step is to get the private key and call the `DecodePrivateKey` method, which in turn is called using the Portable.BouncyCastle library
will decrypt and read the private certificate and return it to us in the form of a private (closed) RSA key in CRT (Chinese Remainder Theorem) format.

If everything is completed successfully, we return a copy of our certificate `CopyWithPrivateKey` but already with data about the private certificate.

You may also have noticed that PemReader receives the password as an instance of the `PasswordFinder` class, which is a requirement of Portable.BouncyCastle, and in order to implement it, we just need to create a separate class that will implement the standard interface of the Portable.BouncyCastle library called `IPasswordFinder`.

```cs
using Org.BouncyCastle.OpenSsl;

internal sealed class PasswordFinder : IPasswordFinder
{
    private readonly string _password;

    public PasswordFinder(string password) => _password = password;

    public char[] GetPassword() => _password.ToCharArray();
}
```

## Conclusion
---
And so we figured out how we can store a certificate in AWS Secrets Manager and use it in our application, which is deployed on AWS Elastic Beanstalk. Also how to use the Portable.BouncyCastle library to decrypt a private certificate.
On the one hand, it's easier than on Azure, because you don't need to do many actions on the AWS portal, but on the other hand, you need to write code that will receive private and public (pem) files separately and generate a certificate base based on this data.
Still, nothing is impossible, and you can always find a solution to solve this or that problem.