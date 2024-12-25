---
title: GitHub Actions - одна із найкращих CI/CD платформ
author: Taras Kovalenko
date: 2024-12-25 12:00:00 +0200
categories: [Git, GitHub, CI/CD, Tips]
tags: [github, tips, git, github actions]
image:
  path: /assets/img/posts/2024-12-25/github_actions.png
---

Не для кого не секрет, що GitHub - це найбільша у світі платформа для спільної розробки програмного забезпечення, яка надає інструменти для контролю версій, управління проєктами та командної співпраці. Вона побудована на базі Git, системи контролю версій, яка дозволяє командам ефективно працювати над кодом, відслідковувати зміни та забезпечувати стабільність програмного забезпечення.
GitHub Actions — це один із ключових інструментів платформи, який дозволяє автоматизувати процеси розробки від перевірки коду до його розгортання. Інтеграція GitHub Actions у ваш робочий процес дозволяє зменшити кількість рутинних завдань, підвищити якість коду та швидкість розробки. У цій статті ми розглянемо основи GitHub Actions, переваги їх використання, а також розглянемо декілька цікавих можливостей які зазвичай потрібні кожному в проєкті, але не всі їх знають.

## Чому варто використовувати Github Actions?

---

* Є частиною GitHub платформи
  
  GitHub Actions тісно інтегровано з GitHub, а це означає, що можливо запускати дії на основі подій, які відбуваються в їхніх сховищах. Це спрощує автоматизацію таких завдань як виконання тестів або розгортання коду в проміжному середовищі.

* Максимальна легкість створення робочих процесів
  
  GitHub Actions надає можливість з легкістю створювати нові процеси для збірки, розгортання та тестування вашого коду. Для того щоб створити новий процес вам достатньо написати декілька рядків простого коду в YAML файлі.

* Гнучка матриця середовищ тестування
  
  Платформа дозволяє легко налаштувати тестування на різних операційних системах та версіях мов програмування. Можна створити матрицю тестів, яка автоматично перевірятиме код на різних конфігураціях, забезпечуючи максимальну сумісність програмного забезпечення.

* Безкоштовне використання для проєктів з відритим кодом

  Для публічних репозиторіїв GitHub Actions надає безкоштовний час виконання та обчислювальні ресурси. Це робить платформу особливо привабливою для open-source проєктів та індивідуальних розробників, які можуть отримати потужні інструменти CI/CD без додаткових витрат.

## Основні компоненти GitHub Actions

---

GitHub Actions складається з декількох ключових компонентів, розуміння яких необхідне для ефективного використання цієї платформи:

### Events (Події)

Події - це специфічні дії в репозиторії, які запускають робочий процес. Найпоширеніші події включають:

* `push` - коли код відправляється в репозиторій
* `pull_request` - при створенні або оновленні pull request
* `release` - коли створюється новий реліз
* `schedule` - запуск за розкладом (використовуючи cron-синтаксис)
* `workflow_dispatch` - ручний запуск процесу

### Workflows (Робочі процеси)

Workflow - це автоматизований процес, який визначається у YAML-файлі в директорії `.github/workflows`.

Кожен workflow може містити:

```yaml
name: CI Process             # Назва процесу
on: [push, pull_request]     # Тригери запуску

jobs:                        # Визначення завдань
  build:                     # Назва job
    runs-on: ubuntu-latest   # Середовище виконання
    steps:                   # Кроки виконання
      - uses: actions/checkout@v4
      - name: Run tests
        run: npm test
```

### Jobs (Завдання)

Jobs визначають послідовність кроків, які виконуються на одному runner:

```yaml
jobs:
  test:                      # Перше завдання
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: npm test

  deploy:                    # Друге завдання
    needs: test              # Залежність від першого
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: npm deploy
```

### Steps (Кроки)

Steps - це індивідуальні завдання всередині job. Приклад різних типів кроків:

```yaml
steps:
  - name: Checkout code           # Ця дія перевіряє ваше сховище в $GITHUB_WORKSPACE, щоб робочий процес мав до нього доступ.
    uses: actions/checkout@v4
    
  - name: Setup .NET             # налаштовує середовище .NET CLI для використання
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 8.0.x
    
  - name: Restore dependencies    # Відновлює залежності та інструменти проєкту
    run: dotnet restore
```

### Runners (Виконавці)

Runners запускають ваші workflows. GitHub надає різні типи:

```yaml
jobs:
  linux-job:
    runs-on: ubuntu-latest    # GitHub-hosted runner
    
  windows-job:
    runs-on: windows-latest   # Windows runner
    
  self-hosted-job:
    runs-on: self-hosted      # Власний runner
```

### Actions (Дії)

Actions - це готові компоненти для типових завдань:

```yaml
steps:
  - uses: actions/checkout@v4     # Клонування репозиторію
  
  - uses: actions/setup-dotnet@v4 # Налаштування .net 8.0.x
    with:
      dotnet-version: 8.0.x
      
  - uses: actions/cache@v4        # Кешування залежностей
    with:
      path: ~/.nuget/packages
      key: ${{ runner.os }}-nuget-${{ hashFiles('**/packages.lock.json') }}
      restore-keys: |
        ${{ runner.os }}-nuget-
```

### Environment (Середовище)

Налаштування середовища через змінні та секрети:

```yaml
jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production     # Визначення середовища
    
    env:                        # Змінні середовища
      APP_ENV: production
      
    steps:
      - name: Deploy to Azure Web App
        id: deploy-to-webapp
        uses: azure/webapps-deploy@v2
        with:
          app-name: ${{ env.AZURE_WEBAPP_NAME }}  # Використання секретних значень
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ${{ env.AZURE_WEBAPP_PACKAGE_PATH }}
```

Розуміння цих компонентів та їх взаємодії дозволяє створювати ефективні та надійні процеси автоматизації.

## Створення першого Workflow для .NET 8

---

Розглянемо створення базового workflow для типового проєкту на .NET 8. Цей приклад демонструє основні можливості GitHub Actions для CI/CD процесу .NET застосунку.

Базова структура workflow

Створимо файл `.github/workflows/dotnet.yml`:

```yaml
name: .NET CI/CD

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
          
      - name: Restore dependencies
        run: dotnet restore
        
      - name: Build
        run: dotnet build --no-restore --configuration Release
        
      - name: Test
        run: dotnet test --no-build --verbosity normal --configuration Release
```

Вищенаведений приклад github action буде автоматично запускатися коли ви створюєте pull request на `main` бранч або коли робите merge, та відповідно запускати збірку вашого проекту і запуск тестів.

### Додаткові налаштування для .NET проєктів

Додавання кешування NuGet пакетів

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```

Налаштування версії .NET SDK

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: |
      6.0.x
      7.0.x
      8.0.x
```

Тестування на різних ОС

```yaml
jobs:
  test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Test
        run: dotnet test
```

Генерація та публікація документації

```yaml
- name: Generate documentation
  run: |
    dotnet tool install -g docfx
    docfx Documentation/docfx.json
    
- name: Publish documentation
  uses: actions/upload-pages-artifact@v3
  with:
    path: Documentation/_site
```

Умовне виконання кроків

Оптимізація через пропуск непотрібних кроків:

```yaml
steps:
  - name: Run Integration Tests
    if: github.ref == 'refs/heads/main' || github.event_name == 'pull_request'
    run: dotnet test --filter Category=Integration

  - name: Deploy to Staging
    if: |
      github.ref == 'refs/heads/develop' && 
      github.event_name == 'push' &&
      !contains(github.event.head_commit.message, '[skip deploy]')
    run: ./deploy-staging.sh
```

Цей workflow забезпечує повний процес CI/CD для .NET 8 проєкту, включаючи:

* Збірку та тестування
* Аналіз якості коду
* Публікацію артефактів
* Розгортання в різні середовища
* Генерацію документації
* Умовне виконання кроків

Ви можете адаптувати його під свої потреби, додаючи або видаляючи кроки залежно від вимог вашого проєкту.
Ці практики допоможуть оптимізувати ваші GitHub Actions workflows, зробивши їх більш ефективними, надійними та легшими в підтримці. Важливо регулярно переглядати та оновлювати ці налаштування відповідно до потреб вашого проєкту.

### Автоматичне скасування GitHub Actions при нових комітах

Коли ми активно працюємо над кодом і робимо багато комітів у гілку, часто виникає ситуація, коли одночасно виконується кілька однакових перевірок на GitHub Actions. Це може бути неефективно, адже нас цікавить результат тільки останнього коміту.

Навіщо це потрібно?
Автоматичне скасування попередніх перевірок дає такі переваги:

* Економить ресурси, якщо ви використовуєте платні ранери
* Зменшує час очікування в черзі для важливих завдань
* Запобігає перевантаженню безкоштовних ранерів (GitHub має обмеження на одночасні запуски)

Як це налаштувати?
GitHub Actions має спеціальний параметр concurrency, який дозволяє групувати та керувати одночасними запусками. 
Ось простий приклад:

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

Цей код створює групу з унікальною назвою для кожної гілки або pull request
Автоматично скасовує попередні запуски при новому коміті

### Особливе налаштування для основної гілки

Часто ми хочемо, щоб перевірки в основній гілці (main) не скасовувались. Для цього можна використати такий варіант:

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref == 'refs/heads/main' && github.run_id || github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

Тепер перевірки будуть скасовуватися тільки в робочих гілках, а в main виконуватимуться всі до кінця.

> Корисна порада

Замість явного зазначення 'main' можна використовувати github.ref_protected. Тоді правило працюватиме для всіх захищених гілок автоматично.
Це налаштування особливо корисне, коли ви:

* Активно працюєте над новим функціоналом
* Часто вносите виправлення
* Маєте обмежені ресурси для CI/CD
* Працюєте в команді з багатьма розробниками

## Висновок

---

GitHub Actions є потужною та гнучкою платформою для автоматизації процесів розробки, що пропонує широкі можливості для створення ефективних CI/CD pipeline. Основні переваги платформи включають:

* Тісну інтеграцію з GitHub екосистемою
* Простоту налаштування через YAML конфігурації
* Багатий вибір готових actions від спільноти
* Підтримку різних операційних систем та середовищ
* Безкоштовність для open-source проєктів

Завдяки детальній документації та активній спільноті, розробники можуть швидко почати використовувати GitHub Actions у своїх проєктах. Платформа корисна для багатьох розробників, надаючи готові рішення для:

* Автоматизації збірки та тестування
* Розгортання застосунків
* Генерації документації
* Керування релізами
* Оптимізації робочих процесів

Використання додаткових функцій, таких як кешування залежностей та автоматичне скасування зайвих workflow запусків, дозволяє ще більше оптимізувати процес розробки та ефективно використовувати доступні ресурси.
GitHub Actions продовжує активно розвиватися, постійно додаючи нові можливості та покращення, що робить цю платформу одним з найкращих рішень для налаштування CI/CD процесів у сучасній розробці програмного забезпечення.
