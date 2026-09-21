---
title: "Source Generators у C#: пишемо маппер моделей під час компіляції"
author: Taras Kovalenko
date: 2025-01-02 12:00:00 +0200
categories: [.net, Roslyn, Source Generator, C#]
tags: [.net, source generator, roslyn, C#]
image:
  path: /assets/img/posts/2025-01-02/source_generators.jpg
---

`Source Generators` з'явилися в `.NET 5`. Це метапрограмування в C#, яке працює під час компіляції: генератор бачить код проєкту і додає до нього новий.

До них код генерували по-різному, і кожен спосіб мав свої обмеження. T4 Templates генерують код ще до компіляції, PostSharp та інші AOP-фреймворки модифікують IL код уже після неї, рефлексія додає overhead під час виконання, а Roslyn Analyzers призначені більше для аналізу, ніж для генерації.

`Source Generators` аналізують код проєкту з повним доступом до семантичної моделі і генерують додатковий код прямо під час компіляції. Так можна автоматизувати рутинні задачі, прибрати рефлексію заради продуктивності і позбутися частини бойлерплейт коду.

## Практичне використання

---

Як приклад напишемо автоматичний маппер моделей. Таку задачу доводиться розв'язувати майже в кожному проєкті.

Наш мапер буде:

* Генерувати код під час компіляції
* Працювати без рефлексії
* Підтримувати базовий маппінг властивостей за іменами
* Підтримувати типобезпечність

## Налаштування проєкту

---

Створюємо Solution з двома проєктами.

```bash
dotnet new sln -n Mapping
dotnet new classlib -o Mapping.SourceGenerators
dotnet new console -o Mapping.Consumer
dotnet sln add Mapping.SourceGenerators
dotnet sln add Mapping.Consumer
```

### Налаштування Generator Project (Mapping.SourceGenerators.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        
        <IsPackable>false</IsPackable>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>
        
        <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
        <IsRoslynComponent>true</IsRoslynComponent>
        
        <RootNamespace>Mapping.SourceGenerators</RootNamespace>
        <PackageId>Mapping.SourceGenerators</PackageId>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0"/>
    </ItemGroup>

</Project>
```

> Три налаштування, які тут мають значення:
{: .prompt-info }

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

Source Generators повинні бути сумісні з `.NET Standard 2.0`, тоді генератор працюватиме з різними версіями .NET.

```xml
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
```

Вмикає додаткові правила перевірки для аналізаторів, які ловлять потенційні проблеми з продуктивністю та сумісністю. Для нових Source Generators його рекомендують вмикати завжди.

```xml
<IsRoslynComponent>true</IsRoslynComponent>
```

Позначає проєкт як компонент компілятора Roslyn. Це вмикає специфічні для Source Generators оптимізації і впливає на те, як генератор завантажується та виконується.

### Важливі NuGet пакети для Source Generators

`Microsoft.CodeAnalysis.Analyzers` містить аналізатори для розробки розширень компілятора, зокрема Source Generators. Вони перевіряють генератор на типові помилки і підказують, як написати його ефективно та без проблем із продуктивністю.

`Microsoft.CodeAnalysis.CSharp` дає доступ до Roslyn Compiler API. Через нього генератор аналізує C# код, працює з синтаксичним деревом і отримує семантичну модель.

### Налаштування Consumer Project (Mapping.Consumer)

```xml
<ItemGroup>
    <ProjectReference Include="..\Mapping.SourceGenerators\Mapping.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false"/>
</ItemGroup>
```

> Що роблять ці два параметри:
{: .prompt-info }

* `OutputItemType="Analyzer"` каже MSBuild, що проєкт є аналізатором коду. Завдяки цьому генератор підключається до процесу компіляції і обробляється правильно.
* `ReferenceOutputAssembly="false"` не дає збірці генератора потрапити в результуючий проєкт. Генератор потрібен тільки під час компіляції, а зайва збірка в посиланнях може спричинити конфлікти типів.

## Реалізація Source Generator

---

### IIncrementalGenerator vs ISourceGenerator

У прикладі ми використовуємо `IIncrementalGenerator` замість старішого `ISourceGenerator`. Інкрементальний генератор обробляє тільки змінені файли, кешує результати між компіляціями і може виконуватися паралельно. У нього також чіткіший API і кращий контроль над життєвим циклом, тому він працює швидше і споживає менше пам'яті.

### Детальний розбір коду

#### Базова структура та атрибути

```cs
[Generator]
public class MappingSourceGenerator : IIncrementalGenerator
{
    // Константи для конфігурації
    private const string Namespace = "Generators";
    private const string AttributeName = "MapFromAttribute";
}
```

Атрибут `Generator` позначає клас як Source Generator для компілятора. Сам клас реалізує `IIncrementalGenerator`, а не `ISourceGenerator`, бо так генератор працює швидше.

#### Генерація атрибута

{% raw %}

```cs
private const string AttributeSourceCode = $@"
namespace {Namespace}
{{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class {AttributeName} : System.Attribute
    {{
        public System.Type SourceType {{ get; }}

        public {AttributeName}(System.Type sourceType)
        {{
            SourceType = sourceType;
        }}
    }}
}}";
```

{% endraw %}

Цей код генерує атрибут, який буде використовуватися для маркування класів, що потребують маппінгу.

#### Ініціалізація генератора

```cs
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    // Реєструємо атрибут
    context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
        $"{AttributeName}.g.cs",
        SourceText.From(AttributeSourceCode, Encoding.UTF8)));

    // Налаштовуємо пайплайн обробки
    var provider = context.SyntaxProvider
        .CreateSyntaxProvider(
            // Швидка перевірка: чи є нод класом?
            (s, _) => s is ClassDeclarationSyntax,
            // Детальний аналіз: перевірка атрибутів
            (ctx, _) => GetClassDeclarationForSourceGen(ctx))
        // Фільтруємо тільки класи з нашим атрибутом
        .Where(t => t.mapFromAttributeFound)
        .Select((t, _) => t.classDeclaration);

    // Реєструємо генерацію коду
    context.RegisterSourceOutput(
        context.CompilationProvider.Combine(provider.Collect()),
        (ctx, t) => GenerateCode(ctx, t.Left, t.Right));
}
```

#### Аналіз синтаксису та пошук атрибутів

```cs
private static (ClassDeclarationSyntax classDeclaration, bool mapFromAttributeFound)
    GetClassDeclarationForSourceGen(GeneratorSyntaxContext context)
{
    // Отримуємо синтаксичне дерево класу
    var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

    // Перебираємо всі атрибути класу
    foreach (var attributeSyntax in classDeclarationSyntax.AttributeLists
        .SelectMany(syntax => syntax.Attributes))
    {
        // Отримуємо інформацію про символ атрибута
        if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol 
            is not IMethodSymbol attributeSymbol)
        {
            continue;
        }

        // Перевіряємо чи це наш атрибут
        string attributeName = attributeSymbol.ContainingType.ToDisplayString();
        if (attributeName == $"{Namespace}.{AttributeName}")
            return (classDeclarationSyntax, true);
    }

    return (classDeclarationSyntax, false);
}
```

#### Генерація коду маппера

```cs
private static void GenerateCode(SourceProductionContext context,
    Compilation compilation,
    ImmutableArray<ClassDeclarationSyntax> classDeclarations)
{
    foreach (var classDeclaration in classDeclarations)
    {
        // Отримуємо семантичну модель
        var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (classSymbol == null) continue;

        // Знаходимо атрибут та отримуємо тип-джерело
        var attribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == AttributeName);

        if (attribute?.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType) 
            continue;

        // Генеруємо код маппера
        string mappingCode = GenerateMappingCode(sourceType, classSymbol);
        context.AddSource(
            $"{sourceType.Name}To{classSymbol.Name}Mapper.g.cs",
            SourceText.From(mappingCode, Encoding.UTF8));
    }
}
```

#### Генерація логіки маппінгу

{% raw %}

```cs
private static string GenerateMappingCode(
  INamedTypeSymbol sourceType, 
  INamedTypeSymbol targetType
)
{
    // Отримуємо всі публічні властивості
    var sourceProperties = sourceType.GetMembers()
        .OfType<IPropertySymbol>()
        .Where(p => p.DeclaredAccessibility == Accessibility.Public)
        .ToList();

    var targetProperties = targetType.GetMembers()
        .OfType<IPropertySymbol>()
        .Where(p => p.DeclaredAccessibility == Accessibility.Public)
        .ToList();

    // Генеруємо код маппінгу властивостей
    var propertyMappings = new StringBuilder();
    foreach (var sourceProp in sourceProperties)
    {
        // Шукаємо відповідну властивість за іменем та типом
        var targetProp = targetProperties.FirstOrDefault(x =>
            x.Name == sourceProp.Name &&
            SymbolEqualityComparer.Default.Equals(x.Type, sourceProp.Type));

        if (targetProp != null)
        {
            propertyMappings.AppendLine(
                $"target.{targetProp.Name} = source.{sourceProp.Name};");
        }
    }

    // Генеруємо кінцевий код маппера
    return $@"
using System;

namespace {Namespace}
{{
    public static class {sourceType.Name}Extensions
    {{
        public static {targetType.Name} MapTo{targetType.Name}(
            this {sourceType.Name} source)
        {{
            if (source == null)
            {{    
                throw new ArgumentNullException(nameof(source));
            }}

            var target = new {targetType.Name}();
            {propertyMappings}
            return target;
        }}
    }}
}}";
}
```

{% endraw %}

## Використання

---

Щоб скористатися генератором, створюємо два класи, між якими хочемо мапити дані, і викликаємо метод розширення. Його назва генерується за патерном `MapTo{targetType.Name}`:

```cs
// Модель даних
public class UserDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }

    public string Surname { get; set; }
}

// Цільова модель з атрибутом для генерації мапера
[MapFrom(typeof(UserDto))]
public class UserViewModel
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Surname { get; set; }
}

// Використання згенерованого коду
var dto = new UserDto { Id = 1, Name = "Taras", Surname = "Kovalenko" };
var viewModel = dto.MapToUserViewModel();
```

Якщо все зроблено правильно, після збірки рішення ви побачите згенерований код маппера, а також атрибут, за яким генератор шукає моделі.

![sg-output](/assets/img/posts/2025-01-02/source_generators_output.png){: width="640" height="480"}

### Переваги Використання Source Generators

Код генерується один раз під час компіляції, тому під час виконання немає ні overhead, ні затримок на рефлексію. Помилки типів виявляються ще на етапі компіляції, IntelliSense повністю бачить згенеровані методи, а рефакторинг нічим не відрізняється від роботи зі звичайним кодом.

Згенерований код можна відкрити й подебажити, як і будь-який інший. Функціональність генератора легко розширювати, а тестувати таке рішення простіше, ніж код на рефлексії.

## Висновок

---

`Source Generators` переносять рутинну роботу з часу виконання на етап компіляції. Платити за це доводиться окремим проєктом генератора і знайомством з Roslyn API. Натомість ви отримуєте швидкий код без рефлексії, перевірку типів під час збірки, нормальну підтримку в IDE і генератор, який можна розширювати під свої задачі.

Порівняно з T4, PostSharp чи рефлексією `Source Generators` швидші й безпечніші, а працювати з ними не складніше.
