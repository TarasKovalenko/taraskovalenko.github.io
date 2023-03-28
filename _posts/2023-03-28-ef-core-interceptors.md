---
title: Перехоплювачі в Entity Framework Core
author: Taras Kovalenko
date: 2023-03-28 12:00:00 +0200
categories: [.net, EntityFramework, Interceptor]
tags: [.net, EntityFramework, EF-Core]
image:
  path: /assets/img/posts/2023-03-28/interceptor.png
---

Під час розробки програмного забезпечення, інколи виникає потреба автоматично модифікувати певні дії, для прикладу автоматично додавати заголовки до HTTP запитів від клієнта до сервера, виконувати певну логіку до чи після певної дії користувача і т.д., зберігати інформацію про ці дії, тощо.
Це все можна робити напряму в тій частині коду яка вам потрібна, але якщо у вас великий проект, то це буде його засмічувати та знову ж таки це буде дублювання коду, а це те що ми не хочемо мати. Так от, щоб автоматизувати даний процес використовують перехоплювачів (interceptors).
Що таке перехоплювач (interceptor)? - це механізм, який дозволяє перехоплювати певні дії, має можливість вносити в них зміни і повернути результат. Перехоплювачі можуть використовуватись для реалізації різних задач, для прикладу логування, перевірка на коректність, зміна значень, і.д..

## Які перехоплювачі існують в Entity Framework Core?
---
В EF-Core існують декілька перехоплювачів, всі вони наслідують інтерфейс `IIinterceptor` який використовувався як базовий для всіх інших інтерфейсів перехоплювачів.

- `ISaveChangesInterceptor` - використовується для перехоплення операції збереження даних.
- `IDbCommandInterceptor` - використовується для перехоплення команди до БД і з можливістю їх змінювати.
- `IDbConnectionInterceptor` - використовується для перехоплення операцій, пов'язаних із з’єднанням до БД DbConnection.
- `IDbTransactionInterceptor` - використовується для перехоплення операцій, пов'язаних із DbTransaction.

Ми будемо використовувати `ISaveChangesInterceptor` із за допомогою якого будемо зберігати додаткову інформацію в БД кожний раз як тільки дані в наших таблицях будуть оновлюватися або створюватися.

Перехоплювачі можуть бути зареєстровані в Entity Framework Core за допомогою методу `AddInterceptors`, який викликається в методі `OnConfiguring` вашого контексту БД `DbContext`. За допомогою цього методу можна зареєструвати один або кілька перехоплювачів.

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.AddInterceptors(_entitySaveChangesInterceptor);

    base.OnConfiguring(optionsBuilder);
}
```

## Створення SaveChangesInterceptor перехоплювача
---
Для початку уявимо, що перед нами стоїть задача створити програму для збереження та редагування інформації про користувачів а також ми повинні мати інформацію хто останній робив зміни в нашій таблиці.
Отже створимо просту таблицю `Users` яка буде містити декілька колонок.

```cs
public class User
{
    public int Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;
}
```

А також напишемо конфігурацію для нашої таблиці:

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

Ми створили клас `User` який буде представляти структуру нашої таблиці, і додали конфігурацію в якій сказали що `Id` це наш первинний ключ, що він має бути унікальним, а також що у нас є ще дві колонки `FirstName` та `LastName` з максимальною довжиною в 250 символів і що вони обов'язкові для заповнення.

Наступним кроком нам потрібно створити ще один клас який буде представляти додаткові колонки для збереження історії змін.

```cs
public class ChangeTrackerEntity
{
    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public int UserId { get; set; }
}
```

і також тепер потрібно щоб клас `User` успадковував `ChangeTrackerEntity`:

```cs
public class User : ChangeTrackerEntity
{
    ...
}
```

Отже ми створили клас який представляє структуру таблиці `User` і також написали конфігурацію, прийшов час реалізувати перехоплювача.
Для цього створимо клас новий `EntitySaveChangesInterceptor` який буде успадковувати `SaveChangesInterceptor`. `SaveChangesInterceptor` - це стандартний клс EF Core який реалізовує інтерфейс `ISaveChangesInterceptor` і надасть нам можливість перехопити всі дії які пов'язані із модифікацією даних в БД.

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
                entry.Entity.UserId = this.userService.GetUserId();
                entry.Entity.CreatedDate = DateTime.UtcNow;
            }

            if (entry.State is EntityState.Added or EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                entry.Entity.UserId = this.userService.GetUserId();
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

І так наш клас успадковує `SaveChangesInterceptor` а також перевизначає два методи `SavingChanges` і `SavingChangesAsync`, вони, в свою чергу, роблять теж саме, перехоплюють модифікацію даних, один синхронно інший асинхронно.

> Будьте уважні, тому що, `SaveChangesInterceptor` також має методи `SavedChanges` та `SavedChangesAsync`, які будуть викликані тільки тоді коли дані вже збережені і в цьому випадку ви не зможете відстежити контекст змін.
{: .prompt-info }

У нас також є `UpdateEntities` метод який отримує всі сутності типу `ChangeTrackerEntity`, які відстежуються контекстом (в нашому випадку тільки таблиця `User`, але ви можете використовувати клас `ChangeTrackerEntity` для інших таблиць і поведінка буде ідентичною), а також перевіряє чи стан сутності `Added` (додати нові дані) або `Modified` (змінити вже існуючі).
Також ми маємо метод розширень `HasChangedOwnedEntities` який перевіряє, чи були змінені або додані власні сутності (owned entities) для конкретної EntityEntry в контексті EF Core. Він перевіряє, чи є в EntityEntry хоча б один Reference, що вказує на owned entity. І після цього для кожного такого Reference перевіряється, чи відбулися зміни в цій owned entity, шляхом перевірки, чи TargetEntry вказує на сутність (entity), яка була додана або змінена (State is EntityState.Added або EntityState.Modified).
Якщо метод повертає true, це означає, що одна або кілька власних сутностей були додані або змінені в поточній EntityEntry.

> Також ми маємо `entry.Entity.UserId = this.userService.GetUserId();` - тут повинна бути логіка яка отримує Id вашого поточного активного користувача, ви можете використовувати DI щоб додати будь які сервіси до перехоплювача.
{: .prompt-info }

Останнім кроком вам потрібно зареєструвати `EntitySaveChangesInterceptor` сервіс в контейнері залежностей (dependency injection container).

```cs
services.AddScoped<EntitySaveChangesInterceptor>();
```

Якщо ми запустимо наш додаток і викличемо метод `SaveChangesAsync` або `SaveChanges` при додаванні або модифікації даних в таблиці `User`, то також автоматично будуть заповненні колонки `CreatedDate`, `ModifiedDate` та `UserId` з відповідними даними.

## Висновок
---
Ми розібралися що таке перехоплювачі і для чого вони потрібні, також як з їх допомогою можна з легкістю реалізувати різного роду логіки і головне не використовуючи безліч дубльовано коду та надлишкових залежностей.
