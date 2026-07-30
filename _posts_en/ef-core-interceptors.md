---
title: Interceptors in Entity Framework Core
author: Taras Kovalenko
date: 2023-03-28 12:00:00.000000000 +02:00
categories:
- ".net"
- EntityFramework
- Interceptor
tags:
- ".net"
- EntityFramework
- EF-Core
image:
  path: "/assets/img/posts/2023-03-28/interceptor.png"
lang: en
locale: en_US
translation_key: ef-core-interceptors
permalink: "/en/posts/ef-core-interceptors/"
---

During software development, sometimes there is a need to automatically modify certain actions, for example, to automatically add headers to HTTP requests from the client to the server, to perform certain logic before or after a certain user action, etc., to store information about these actions, etc.
All this can be done directly in the part of the code that you need, but if you have a large project, then it will clutter it and again it will be duplication of code, which is something we do not want to have. So, to automate this process, interceptors are used.
What is an interceptor? - is a mechanism that allows you to intercept certain actions, has the ability to make changes to them and return the result. Interceptors can be used to implement various tasks, for example, logging, checking for correctness, changing values, etc.

## What interceptors exist in Entity Framework Core?
---
There are several interceptors in EF-Core, all of which emulate the `IIinterceptor` interface that was used as the base for all other interceptor interfaces.

- `ISaveChangesInterceptor` - used to intercept data saving operation.
- `IDbCommandInterceptor` - used to intercept commands to the database and with the possibility of changing them.
- `IDbConnectionInterceptor` - used to intercept operations related to connection to the DbConnection database.
- `IDbTransactionInterceptor` - used to intercept operations related to DbTransaction.

We will use `ISaveChangesInterceptor` with which we will store additional information in the database every time the data in our tables is updated or created.

Interceptors can be registered in Entity Framework Core using the `AddInterceptors` method, which is called in the `OnConfiguring` method of your `DbContext` DB context. You can register one or more interceptors using this method.

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.AddInterceptors(_entitySaveChangesInterceptor);

    base.OnConfiguring(optionsBuilder);
}
```

## Create a SaveChangesInterceptor interceptor
---
To begin with, let's imagine that we are faced with the task of creating a program for saving and editing information about users, and we also need to have information about who last made changes to our table.
So, let's create a simple table `Users` that will contain several columns.

```cs
public class User
{
    public int Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;
}
```

And also write the configuration for our table:

```cs
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Id).IsUnique();
        builder.Property(x => x.FirstName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(250).IsRequired();
    }
}
```

We created a class `User` that will represent the structure of our table, and added a configuration that said that `Id` is our primary key, that it must be unique, and that we have two more columns `FirstName` and `LastName` with a maximum length of 250 characters and that they are required to be filled.

The next step is to create another class that will represent additional columns for saving the history of changes.

```cs
public class ChangeTrackerEntity
{
    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public int ModifierId { get; set; }
}
```

and also now require that the class `User` inherits `ChangeTrackerEntity`:

```cs
public class User : ChangeTrackerEntity
{
    ...
}
```

So, we created a class that represents the structure of the `User` table and also wrote the configuration, it's time to implement the interceptor.
To do this, we will create a new class `EntitySaveChangesInterceptor` that will inherit `SaveChangesInterceptor`. `SaveChangesInterceptor` is a standard EF Core class that implements the `ISaveChangesInterceptor` interface and will give us the opportunity to intercept all actions related to data modification in the database.

```cs
public class EntitySaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ChangeTrackerEntity>())
        {
            if (entry.State is EntityState.Added)
            {
                entry.Entity.ModifierId = this.userService.GetUserId();
                entry.Entity.CreatedDate = DateTime.UtcNow;
            }

            if (entry.State is EntityState.Added or EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                entry.Entity.ModifierId = this.userService.GetUserId();
                entry.Entity.ModifiedDate = DateTime.UtcNow;
            }
        }
    }
}

public static class Extensions
{
    public static bool HasChangedOwnedEntities(this EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry != null &&
            r.TargetEntry.Metadata.IsOwned() &&
            r.TargetEntry.State is EntityState.Added or EntityState.Modified);
}
```

And so our class inherits `SaveChangesInterceptor` and also overrides two methods `SavingChanges` and `SavingChangesAsync`, they, in turn, do the same, intercept data modification, one synchronously and the other asynchronously.

> Be careful because `SaveChangesInterceptor` also has methods `SavedChanges` and `SavedChangesAsync` which will be called only when the data is already saved and in this case you won't be able to track the context of the changes.
{: .prompt-info }

We also have a `UpdateEntities` method that gets all entities of type `ChangeTrackerEntity` tracked by the context (in our case only the `User` table, but you can use the `ChangeTrackerEntity` class for other tables and the behavior will be identical), and checks whether the state of the entity is `Added` (add new data) or `Modified` (change existing ones).
We also have an extension method `HasChangedOwnedEntities` that checks whether owned entities have been changed or added for a specific EntityEntry in the context of EF Core. It checks if the EntityEntry has at least one Reference that points to the owned entity. And after that, for each such Reference, it is checked whether changes have occurred in this owned entity, by checking whether the TargetEntry points to an entity that was added or modified (State is EntityState.Added or EntityState.Modified).
If the method returns true, it means that one or more custom entities have been added or modified in the current EntityEntry.

> Also we have `entry.Entity.ModifierId = this.userService.GetUserId();` - there should be logic here that gets the Id of your current active user, you can use DI to add any services to the interceptor.
{: .prompt-info }

The last step is to register the `EntitySaveChangesInterceptor` service in the dependency injection container.

```cs
services.AddScoped<EntitySaveChangesInterceptor>();
```

If we run our application and call the `SaveChangesAsync` or `SaveChanges` method when adding or modifying data in the `User` table, the columns `CreatedDate`, `ModifiedDate` and `ModifierId` will also be automatically filled with the corresponding data.

## Conclusion
---
We figured out what interceptors are and why they are needed, as well as how with their help you can easily implement various types of logic and most importantly without using a lot of duplicated code and redundant dependencies.