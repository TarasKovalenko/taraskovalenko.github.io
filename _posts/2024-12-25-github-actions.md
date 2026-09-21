---
title: GitHub Actions - одна із найкращих CI/CD платформ
author: Taras Kovalenko
date: 2024-12-25 12:00:00 +0200
categories: [Git, GitHub, CI/CD, Tips]
tags: [github, tips, git, github actions]
image:
  path: /assets/img/posts/2024-12-25/github_actions.png
---

GitHub - найбільша у світі платформа для спільної розробки програмного забезпечення з інструментами для контролю версій, управління проєктами та командної роботи. В її основі Git, система контролю версій, яка дозволяє командам спільно працювати над кодом і відстежувати зміни.
GitHub Actions - вбудований у платформу інструмент для автоматизації розробки, від перевірки коду до його розгортання. Він знімає з команди рутину і допомагає тримати якість коду та швидкість розробки. Нижче - основи GitHub Actions, чому ними варто користуватися, і кілька можливостей, які потрібні майже в кожному проєкті, хоча знають про них не всі.

## Чому варто використовувати Github Actions?

---

Головна перевага GitHub Actions у тому, що він є частиною GitHub. Дії запускаються на події, які відбуваються в репозиторії, тож автоматизувати, наприклад, запуск тестів або розгортання коду в проміжне середовище дуже просто.

Щоб створити новий процес збірки, тестування чи розгортання, достатньо кількох рядків YAML. А матриця тестів автоматично перевіряє код на різних операційних системах і версіях мов програмування, тож сумісність видно одразу.

Для публічних репозиторіїв час виконання та обчислювальні ресурси безкоштовні, тому open-source проєкти та індивідуальні розробники отримують повноцінний CI/CD без додаткових витрат.

## Основні компоненти GitHub Actions

---

Ось з чого складається GitHub Actions:

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

{% raw %}
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
{% endraw %}

### Environment (Середовище)

Налаштування середовища через змінні та секрети:

{% raw %}
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
{% endraw %}



## Створення першого Workflow для .NET 8

---

Почнемо з базового workflow для типового проєкту на .NET 8.

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

Цей workflow запускається, коли ви створюєте pull request у `main` або робите merge: він збирає проєкт і запускає тести.

### Додаткові налаштування для .NET проєктів

Додавання кешування NuGet пакетів

{% raw %}
```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```
{% endraw %}

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

{% raw %}
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
{% endraw %}

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

{% raw %}
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
{% endraw %}

Разом ці кроки дають повний CI/CD цикл для .NET 8 проєкту: збірка й тестування, аналіз якості коду, публікація артефактів, розгортання в різні середовища, генерація документації та умовне виконання кроків.

Адаптуйте його під свій проєкт, додаючи або прибираючи кроки, і час від часу переглядайте ці налаштування, коли змінюються потреби проєкту.

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

{% raw %}
```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```
{% endraw %}

Цей код створює групу з унікальною назвою для кожної гілки або pull request і автоматично скасовує попередні запуски при новому коміті.

### Особливе налаштування для основної гілки

Часто ми хочемо, щоб перевірки в основній гілці (main) не скасовувались. Для цього можна використати такий варіант:

{% raw %}
```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref == 'refs/heads/main' && github.run_id || github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```
{% endraw %}

Тепер перевірки будуть скасовуватися тільки в робочих гілках, а в main виконуватимуться всі до кінця.

> Корисна порада
{: .prompt-info }

Замість явного зазначення 'main' можна використовувати github.ref_protected. Тоді правило працюватиме для всіх захищених гілок автоматично.
Це налаштування особливо корисне, коли ви:

* Активно працюєте над новим функціоналом
* Часто вносите виправлення
* Маєте обмежені ресурси для CI/CD
* Працюєте в команді з багатьма розробниками

## Висновок

---

GitHub Actions зручний насамперед тому, що вбудований у GitHub. Він налаштовується звичайним YAML, працює на різних операційних системах і в різних середовищах, безкоштовний для open-source і має великий вибір готових actions від спільноти. Цього вистачає на збірку й тестування, розгортання застосунків, генерацію документації та керування релізами.

Кешування залежностей та автоматичне скасування зайвих запусків workflow додатково економлять час і ресурси, тож не нехтуйте ними. А оскільки платформа активно розвивається, час від часу варто перевіряти, що в ній з'явилось нового.
