---
title: Source Generators in C# - from theory to practice
author: Taras Kovalenko
date: 2025-01-02 12:00:00.000000000 +02:00
categories:
- ".net"
- Roslyn
- Source Generator
- C#
tags:
- ".net"
- source generator
- roslyn
- C#
image:
  path: "/assets/img/posts/2025-01-02/source_generators.jpg"
lang: en
locale: en_US
translation_key: source-generators
permalink: "/en/posts/source-generators/"
---

`Source Generators` were introduced in `.NET 5` as a compile-time code generation tool, a compile-time metaprogramming tool in C#.
Before their appearance, developers used different approaches to code generation:

* T4 Templates
* PostSharp and other AOP frameworks
* Reflection during execution
* Roslyn Analyzers

Each of these approaches had its limitations:

* T4 Templates generate pre-compiled code
* PostSharp modifies IL code after compilation
* Reflection has overhead at runtime
* Roslyn Analyzers are designed more for analysis than generation

`Source Generators` solves these problems, which allows us to analyze the project code and generate additional code at compile time, with full access to the semantic model of the code.
This enables us to:

* Automate routine tasks
* Improve performance by avoiding reflection
* Reduce the amount of boilerplate code

## Practical use

---

In this article, we will consider the creation of an automatic model mapper - a fairly frequent task in modern software development. 

Our mapper will be:

* Generate code at compile time
* Work without reflection
* Support basic mapping of properties by name
* Maintain type safety

## Project settings

---

To begin with, we need to create a new Solution with two projects.

```bash
dotnet new sln -n Mapping
dotnet new classlib -o Mapping.SourceGenerators
dotnet new console -o Mapping.Consumer
dotnet sln add Mapping.SourceGenerators
dotnet sln add Mapping.Consumer
```

### Generator Project settings (Mapping.SourceGenerators.csproj)

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

> Important settings and their purpose:
{: .prompt-info }

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

Source Generators must be compatible with `.NET Standard 2.0`

This provides broad compatibility with different versions of .NET

```xml
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
```

Enables additional validation rules for parsers

Helps identify potential performance and compatibility issues

Recommended for all new Source Generators

```xml
<IsRoslynComponent>true</IsRoslynComponent>
```

Marks the project as a component of the Roslyn compiler

Enables specific optimizations for Source Generators

Affects the process of loading and executing the generator

### Important NuGet packages for Source Generators

`Microsoft.CodeAnalysis.Analyzers` is a package that contains a set of analyzers for developing compiler extensions, including Source Generators. 

It provides:

* Rules and guidelines for writing efficient generators
* Checks for common errors
* Performance optimization

`Microsoft.CodeAnalysis.CSharp` - provides access to the Roslyn Compiler API, which allows:

* Analyze C# code
* Work with the syntax tree
* Get a semantic model

### Consumer Project settings (Mapping.Consumer)

```xml
<ItemGroup>
    <ProjectReference Include="..\Mapping.SourceGenerators\Mapping.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false"/>
</ItemGroup>
```

> Key parameters:
{: .prompt-info }

* `OutputItemType="Analyzer"`
  * Indicates that the project is a code analyzer
  * Integrates the generator into the compilation process
  * Allows MSBuild to handle the generator correctly
  
* `ReferenceOutputAssembly="false"`
  * Prevents the generator assembly from being included in the resulting project
  * Important to avoid type conflicts
  * The generator is used only during compilation

## Implementation of Source Generator

---

### IIncrementalGenerator vs ISourceGenerator

In the example, we use `IIncrementalGenerator` instead of the older `ISourceGenerator`.

> Key advantages:

* Incremental generation:
  * Processes only modified files
  * Caches results between builds
  * Supports parallel execution

* Better control over the life cycle:
  * Clearer API
  * Better performance
  * Less memory consumption

### Detailed analysis of the code

#### Basic structure and attributes

```cs
[Generator]
public class MappingSourceGenerator : IIncrementalGenerator
{
    // Constants for configuration
    private const string Namespace = "Generators";
    private const string AttributeName = "MapFromAttribute";
}
```

Attribute `Generator` - marks the class as a Source Generator for the compiler and we also use `IIncrementalGenerator` for better performance compared to `ISourceGenerator`

#### Attribute generation

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

This code generates an attribute that will be used to mark classes that require mapping.

#### Generator initialization

```cs
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    // We register the attribute
    context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
        $"{AttributeName}.g.cs",
        SourceText.From(AttributeSourceCode, Encoding.UTF8)));

    // We configure the processing pipeline
    var provider = context.SyntaxProvider
        .CreateSyntaxProvider(
            // Quick check: Is a node a class?
            (s, _) => s is ClassDeclarationSyntax,
            // Detailed analysis: checking attributes
            (ctx, _) => GetClassDeclarationForSourceGen(ctx))
        // Filter only classes with our attribute
        .Where(t => t.mapFromAttributeFound)
        .Select((t, _) => t.classDeclaration);

    // Register code generation
    context.RegisterSourceOutput(
        context.CompilationProvider.Combine(provider.Collect()),
        (ctx, t) => GenerateCode(ctx, t.Left, t.Right));
}
```

#### Syntax analysis and attribute search

```cs
private static (ClassDeclarationSyntax classDeclaration, bool mapFromAttributeFound)
    GetClassDeclarationForSourceGen(GeneratorSyntaxContext context)
{
    // We get the syntactic tree of the class
    var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

    // We go through all the attributes of the class
    foreach (var attributeSyntax in classDeclarationSyntax.AttributeLists
        .SelectMany(syntax => syntax.Attributes))
    {
        // We get information about the attribute symbol
        if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol 
            is not IMethodSymbol attributeSymbol)
        {
            continue;
        }

        // We check whether this is our attribute
        string attributeName = attributeSymbol.ContainingType.ToDisplayString();
        if (attributeName == $"{Namespace}.{AttributeName}")
            return (classDeclarationSyntax, true);
    }

    return (classDeclarationSyntax, false);
}
```

#### Generating the mapper code

```cs
private static void GenerateCode(SourceProductionContext context,
    Compilation compilation,
    ImmutableArray<ClassDeclarationSyntax> classDeclarations)
{
    foreach (var classDeclaration in classDeclarations)
    {
        // We get a semantic model
        var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (classSymbol == null) continue;

        // We find the attribute and get the source type
        var attribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == AttributeName);

        if (attribute?.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType) 
            continue;

        // We generate the mapper code
        string mappingCode = GenerateMappingCode(sourceType, classSymbol);
        context.AddSource(
            $"{sourceType.Name}To{classSymbol.Name}Mapper.g.cs",
            SourceText.From(mappingCode, Encoding.UTF8));
    }
}
```

#### Generation of mapping logic

{% raw %}

```cs
private static string GenerateMappingCode(
  INamedTypeSymbol sourceType, 
  INamedTypeSymbol targetType
)
{
    // We get all public properties
    var sourceProperties = sourceType.GetMembers()
        .OfType<IPropertySymbol>()
        .Where(p => p.DeclaredAccessibility == Accessibility.Public)
        .ToList();

    var targetProperties = targetType.GetMembers()
        .OfType<IPropertySymbol>()
        .Where(p => p.DeclaredAccessibility == Accessibility.Public)
        .ToList();

    // We generate the property mapping code
    var propertyMappings = new StringBuilder();
    foreach (var sourceProp in sourceProperties)
    {
        // We are looking for a suitable property by name and type
        var targetProp = targetProperties.FirstOrDefault(x =>
            x.Name == sourceProp.Name &&
            SymbolEqualityComparer.Default.Equals(x.Type, sourceProp.Type));

        if (targetProp != null)
        {
            propertyMappings.AppendLine(
                $"target.{targetProp.Name} = source.{sourceProp.Name};");
        }
    }

    // We generate the final mapper code
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

## Usage

---

For use, we need to create two classes between which we want to map data and call the extension method that will be generated automatically according to the following pattern `MapTo{targetType.Name}`:

```cs
// Data model
public class UserDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }

    public string Surname { get; set; }
}

// A target model with an attribute to generate the mapper
[MapFrom(typeof(UserDto))]
public class UserViewModel
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Surname { get; set; }
}

// Using generated code
var dto = new UserDto { Id = 1, Name = "Taras", Surname = "Kovalenko" };
var viewModel = dto.MapToUserViewModel();
```

If everything is done correctly, after assembling the solution, you should see the generated code for mapping and also the attribute by which models are searched

![sg-output](/assets/img/posts/2025-01-02/source_generators_output.png){: width="640" height="480"}

### Advantages of using Source Generators

* Productivity:
  * Zero overhead during execution
  * Code is generated once during compilation
  * There are no reflection delays

* Type safety:
  * Errors are detected at the compilation stage
  * Full IntelliSense support
  * Easy refactoring

* Support:
  * Generated code can be viewed and debugged
  * Easy to extend functionality
  * Easier testing

## Conclusion

---

`Source Generators` is a powerful tool for automating routine tasks in .NET development.

They provide:

* High performance due to compile-time generation
* Type safety and excellent integration with IDE
* Flexibility in expansion and modification

Compared to traditional approaches, `Source Generators` offer a better balance between performance, security and usability.