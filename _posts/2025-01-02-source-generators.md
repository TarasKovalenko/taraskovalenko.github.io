---
title: Source Generators в C# - від теорії до практики
author: Taras Kovalenko
date: 2025-01-02 12:00:00 +0200
categories: [.net, Roslyn, Source Generator, C#]
tags: [.net, source generator, roslyn, C#]
image:
  path: /assets/img/posts/2025-01-02/source_generators.jpg
---

`Source Generators` були представлені в `.NET 5` як інструмент для генерації коду під час компіляції, це інструмент для метапрограмування під час компіляції в C#.
До їх появи розробники використовували різні підходи для генерації коду:

* T4 Templates
* PostSharp та інші AOP-фреймворки
* Рефлексія під час виконання
* Roslyn Analyzers

Кожен з цих підходів мав свої обмеження:

* T4 Templates генерують код до компіляції
* PostSharp модифікує IL код після компіляції
* Рефлексія має overhead під час виконання
* Roslyn Analyzers призначені більше для аналізу, ніж для генерації

`Source Generators` вирішують ці проблеми, яка дозволяє нам аналізувати код проєкту та генерувати додатковий код під час компіляції, з повним доступом до семантичної моделі коду.
Це дає нам можливість:

* Автоматизувати рутинні задачі
* Покращити продуктивність, уникаючи рефлексії
* Зменшити кількість бойлерплейт коду

## Практичне використання

---

У цій статті ми розглянемо створення автоматичного маппера моделей - доволі частої задачі в сучасній розробці ПЗ. 

Наш мапер буде:

* Генерувати код під час компіляції
* Працювати без рефлексії
* Підтримувати базовий маппінг властивостей за іменами
* Підтримувати типобезпечність

## Налаштування проєкту

---

Для початку нам потрібно створити новий Solution з двома проєктами.

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

> Важливі налаштування та їх призначення:
{: .prompt-info }

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

Source Generators повинні бути сумісні з `.NET Standard 2.0`

Це забезпечує широку сумісність з різними версіями .NET

```xml
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
```

Вмикає додаткові правила перевірки для аналізаторів

Допомагає виявити потенційні проблеми з продуктивністю та сумісністю

Рекомендується для всіх нових Source Generators

```xml
<IsRoslynComponent>true</IsRoslynComponent>
```

Позначає проект як компонент компілятора Roslyn

Активує специфічні оптимізації для Source Generators

Впливає на процес завантаження та виконання генератора

### Важливі NuGet пакети для Source Generators

`Microsoft.CodeAnalysis.Analyzers` - це пакет, який містить набір аналізаторів для розробки компіляторних розширень, включаючи Source Generators. 

Він забезпечує:

* Правила та рекомендації для написання ефективних генераторів
* Перевірки на типові помилки
* Оптимізацію продуктивності

`Microsoft.CodeAnalysis.CSharp` - надає доступ до Roslyn Compiler API, що дозволяє:

* Аналізувати C# код
* Працювати з синтаксичним деревом
* Отримувати семантичну модель

### Налаштування Consumer Project (Mapping.Consumer)

```xml
<ItemGroup>
    <ProjectReference Include="..\Mapping.SourceGenerators\Mapping.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false"/>
</ItemGroup>
```

> Ключові параметри:
{: .prompt-info }

* `OutputItemType="Analyzer"`
  * Вказує, що проект є аналізатором коду
  * Інтегрує генератор в процес компіляції
  * Дозволяє MSBuild правильно обробляти генератор
  
* `ReferenceOutputAssembly="false"`
  * Запобігає включенню збірки генератора в результуючий проєкт
  * Важливо для уникнення конфліктів типів
  * Генератор використовується тільки під час компіляції

## Реалізація Source Generator

---

### IIncrementalGenerator vs ISourceGenerator

У прикладі ми використовуємо `IIncrementalGenerator` замість старішого `ISourceGenerator`.

> Ключові переваги:

* Інкрементальна генерація:
  * Обробляє тільки змінені файли
  * Кешує результати між компіляціями
  * Підтримує паралельне виконання

* Кращий контроль над життєвим циклом:
  * Чіткіший API
  * Краща продуктивність
  * Менше споживання пам'яті

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

Атрибут `Generator` - маркує клас як Source Generator для компілятора і також використовуємо `IIncrementalGenerator` для кращої продуктивності в порівнянні із `ISourceGenerator`

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

Для використання нам потрібно створити два класи між якими ми хочемо мапити дані та викликати метод розширення який буде згенеровано автоматично за наступним патерном `MapTo{targetType.Name}`:

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

Якщо все вірно зроблено то після збірки рішення ви повинні побачити згенерований код для мапінгу і також атрибут по якому і відбувається пошук моделей

![sg-output](/assets/img/posts/2025-01-02/source_generators_output.png){: width="640" height="480"}

### Переваги Використання Source Generators

* Продуктивність:
  * Нульовий overhead під час виконання
  * Код генерується один раз під час компіляції
  * Немає затримок на рефлексію

* Типобезпека:
  * Помилки виявляються на етапі компіляції
  * Повна підтримка IntelliSense
  * Легке рефакторинг

* Підтримка:
  * Згенерований код можна переглядати та дебажити
  * Легко розширювати функціональність
  * Простіше тестування

## Висновок

---

`Source Generators` - це потужний інструмент для автоматизації рутинних задач в .NET розробці.

Вони надають:

* Високу продуктивність завдяки генерації під час компіляції
* Типобезпеку та відмінну інтеграцію з IDE
* Гнучкість у розширенні та модифікації

У порівнянні з традиційними підходами, `Source Generators` пропонують кращий баланс між продуктивністю, безпекою та зручністю використання.
