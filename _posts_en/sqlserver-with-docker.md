---
title: Connecting to a locally installed SQL Server from a Docker container
author: Taras Kovalenko
date: 2023-09-09 12:00:00.000000000 +02:00
categories:
- ".net"
- EntityFramework
- Interceptor
tags:
- ".net"
- SQLServer
- Docker
image:
  path: "/assets/img/posts/2023-09-09/docker.jpeg"
lang: en
locale: en_US
translation_key: sqlserver-with-docker
permalink: "/en/posts/sqlserver-with-docker/"
---

In recent decades, the field of information technology has experienced a revolution in the deployment and management of software applications. Startups and corporations, developers and administrators, are all familiar with the security, scalability, and reliability challenges of software applications. This is where Docker enters the scene, which has become a real revolution in the field of containerization.
In this article, I will show how to connect from a Docker container to a locally installed MS SQL Server (*on your PC, not in the middle of a Docker container*).

## Introduction
---
For example, I have a `.NET core` application that uses a `SQL` database. I use `EF core` to work with the database and I have the following settings for connecting to the database:

```json
"ConnectionStrings": {
  "DatabaseConnection": "Server=.\\SQLEXPRESS;Database=dotnetapp;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;"
}
```

Everything is simple here, we have:

1. `Server=.\SQLEXPRESS` - indicates the instance of the server or database to connect to, in our case the local instance of SQL Server Express.
2. `Database=dotnetapp` - database name.
3. `Trusted_Connection=True` - Indicates that Windows authentication (also known as Trusted_Connection) should be used to establish the connection. If Trusted_Connection is set to True, it means that the credentials of the currently logged on user will be used to authenticate and connect to the database.
4. `MultipleActiveResultSets=true` - allows you to simultaneously perform several queries on one connection to the database.
5. `Encrypt=False` - this means that the connection between the program and SQL Server will not be encrypted (should be used only for local development).

If we run the application, everything will work fine, but if we deploy the application in a Docker container, we will get an error that we cannot connect to the database.

## Configuring the TCP/IP protocol for SQL Server
---

A Docker container runs as a separate machine on your host machine, so to connect to a SQL database on your host, you need to enable remote SQL server connections.
To do this, you need to open `SQL Server Configuration Manager`, which is located at the following address `C:\Windows\SysWOW64\SQLServerManager15.msc`, open `SQL Server Network Configuration` -> `Protocols for SQLEXPRESS` and enable the `TCP/IP` protocol. You also need to make sure that `TCP Port` is not empty.

![sql-tcp-ip](/assets/img/posts/2023-09-09/sql-tcp-ip.png){: width="640" height="480"}

After configuring the `TCP/IP` protocol, you need to restart the `SQL Server` service.
To do this, you need to go to the `SQL Server Services` section, select `SQL Server (SQLEXPRESS)` and restart it.

You also need to make sure that the `SQL Server Browser` service is running, if it is not, start it. `SQL Server Browser` is used by Microsoft SQL Server to provide information about the location of various instances of `SQL Server`` on your network and simplifies the process of connecting client applications to SQL Server, especially in situations where you have many instances of SQL Server on the same server or network.

![sql-server-browser](/assets/img/posts/2023-09-09/sql-server-browser.png){: width="640" height="480"}

In general, this is all you need to do to configure the `TCP/IP` protocol, but to avoid problems, make sure that remote connections to your `SQL Server` are enabled.
Open `Microsoft SQL Server Management Studio`, connect to the server and then open its properties.

![sql-server-remote-connections](/assets/img/posts/2023-09-09/sql-server-remote-connections.png){: width="640" height="480"}

## Changing the connection string to the database
---

To connect to the locally installed `SQL Server` from the Docker container, you need to change the path to the server, in our case, instead of `\SQLEXPRESS`, we must specify the address of our host machine. To do this, you need to use `host.docker.internal` - this is a special DNS name provided by Docker to allow the container to connect to services running on the host machine (your PC in our case), and you need to specify the `TCP/IP` port of your SQL Server.

As a result, our connection string will look like this:

```json
"ConnectionStrings": {
  "DatabaseConnection": "Server=host.docker.internal,48475;Database=dotnetapp;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;"
}
```

But still, if you run the application in a Docker container, it will not work and you will get the following error:

```shell
dotnetcore : Cannot access Kerberos ticket. Ensure Kerberos has been initialized with 'kinit'...
```

What is your problem? The problem is that we are using `Trusted_Connection=True` - which indicates that Windows authentication should be used to establish a connection. The Docker container does not have access to Windows authentication information and therefore cannot connect you to the database.

You need to [create a new SQL Server connection name](https://learn.microsoft.com/en-us/sql/relational-databases/security/authentication-access/create-a-login) and grant access to `dotnetapp`, and replace `Trusted_Connection=True` with `User=<YOUR_ID>;Password=<YOUR_PWD>`.

The final connection line should look like this:

```json
"ConnectionStrings": {
  "DatabaseConnection": "Server=host.docker.internal,48475;Database=dotnetapp;User=Admin;Password=Pa$$w0rd;MultipleActiveResultSets=true;Encrypt=False;"
}
```

Now, if you run the application in a Docker container, everything will work and you will have access to SQL Server installed on your local PC.

## Conclusion
---
In this article, we considered an effective way to connect to a locally installed SQL Server server from a Docker container. Using a sample connection string, we learned how to configure parameters to ensure a successful connection by specifying the server, port, username and password, and database name.

This article covered the key aspects of setting up a database connection using a connection string and provided an understanding of how a Docker container communicates with a SQL Server server. By following these guidelines, you can effectively work with SQL Server in a virtual container environment for developing and testing your software.