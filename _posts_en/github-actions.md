---
title: GitHub Actions is one of the best CI/CD platforms out there
author: Taras Kovalenko
date: 2024-12-25 12:00:00.000000000 +02:00
categories:
- Git
- GitHub
- CI/CD
- Tips
tags:
- github
- tips
- git
- github actions
image:
  path: "/assets/img/posts/2024-12-25/github_actions.png"
lang: en
locale: en_US
translation_key: github-actions
permalink: "/en/posts/github-actions/"
---

It's no secret that GitHub is the world's largest collaborative software development platform that provides tools for version control, project management, and team collaboration. It is built on top of Git, a version control system that allows teams to efficiently work on code, track changes, and ensure software stability.
GitHub Actions is one of the platform's key tools that allows you to automate development processes from code review to deployment. Integrating GitHub Actions into your workflow reduces routine tasks, improves code quality, and speeds up development. In this article, we will look at the basics of GitHub Actions, the benefits of using them, and also consider some interesting features that are usually needed by everyone in the project, but not everyone knows about them.

## Why use Github Actions?

---

* Is part of the GitHub platform
  
  GitHub Actions is tightly integrated with GitHub, which means it's possible to trigger actions based on events that occur in their repositories. This makes it easy to automate tasks like running tests or deploying code to a staging environment.

* Maximum ease of creation of workflows
  
  GitHub Actions makes it easy to create new processes for building, deploying, and testing your code. In order to create a new process, you just need to write a few lines of simple code in a YAML file.

* A flexible matrix of testing environments
  
  The platform allows you to easily set up testing on different operating systems and versions of programming languages. A test matrix can be created that will automatically test code on different configurations, ensuring maximum software compatibility.

* Free to use for open source projects

  For public repositories, GitHub Actions provides free runtime and computing resources. This makes the platform particularly attractive for open-source projects and individual developers, who can get powerful CI/CD tools at no additional cost.

## Core components of GitHub Actions

---

GitHub Actions consists of several key components that need to be understood in order to use this platform effectively:

### Events

Events are specific actions in the repository that trigger a workflow. The most common events include:

* `push` - when the code is sent to the repository
* `pull_request` - when creating or updating a pull request
* `release` - when a new release is created
* `schedule` - start on schedule (using cron syntax)
* `workflow_dispatch` - manual start of the process

### Workflows

Workflow is an automated process defined in a YAML file in the `.github/workflows` directory.

Each workflow can contain:

```yaml
name: CI Process             # The name of the process
on: [push, pull_request]     # Launch triggers

jobs:                        # Definition of tasks
  build:                     # Job name
    runs-on: ubuntu-latest   # Execution environment
    steps:                   # Implementation steps
      - uses: actions/checkout@v4
      - name: Run tests
        run: npm test
```

### Jobs

Jobs define a sequence of steps that are performed on one runner:

```yaml
jobs:
  test:                      # The first task
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: npm test

  deploy:                    # The second task
    needs: test              # Dependence on the first
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: npm deploy
```

### Steps

Steps are individual tasks within a job. Example of different types of steps:

```yaml
steps:
  - name: Checkout code           # This action checks your repository in $GITHUB_WORKSPACE for the workflow to access.
    uses: actions/checkout@v4
    
  - name: Setup .NET             # configures the .NET CLI environment to use
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 8.0.x
    
  - name: Restore dependencies    # Restores project dependencies and tools
    run: dotnet restore
```

### Runners

Runners run your workflows. GitHub provides different types:

```yaml
jobs:
  linux-job:
    runs-on: ubuntu-latest    # GitHub-hosted runner
    
  windows-job:
    runs-on: windows-latest   # Windows runner
    
  self-hosted-job:
    runs-on: self-hosted      # Own runner
```

### Actions

Actions are ready-made components for typical tasks:

{% raw %}

```yaml
steps:
  - uses: actions/checkout@v4     # Cloning the repository
  
  - uses: actions/setup-dotnet@v4 # Setting up .NET 8.0.x
    with:
      dotnet-version: 8.0.x
      
  - uses: actions/cache@v4        # Dependency caching
    with:
      path: ~/.nuget/packages
      key: ${{ runner.os }}-nuget-${{ hashFiles('**/packages.lock.json') }}
      restore-keys: |
        ${{ runner.os }}-nuget-
```

{% endraw %}

### Environment

Setting up the environment through variables and secrets:

{% raw %}

```yaml
jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production     # Definition of environment
    
    env:                        # Environment variables
      APP_ENV: production
      
    steps:
      - name: Deploy to Azure Web App
        id: deploy-to-webapp
        uses: azure/webapps-deploy@v2
        with:
          app-name: ${{ env.AZURE_WEBAPP_NAME }}  # Using secret values
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ${{ env.AZURE_WEBAPP_PACKAGE_PATH }}
```

{% endraw %}

Understanding these components and their interaction allows you to create efficient and reliable automation processes.

## Creation of the first Workflow for .NET 8

---

Consider creating a basic workflow for a typical project on .NET 8. This example demonstrates the basic capabilities of GitHub Actions for the CI/CD process of a .NET application.

Basic workflow structure

Let's create a file `.github/workflows/dotnet.yml`:

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

The above example github action will automatically run when you create a pull request on the `main` branch or when you do a merge, and accordingly start building your project and running tests.

### Additional settings for .NET projects

Adding NuGet package caching

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

Setting the .NET SDK version

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: |
      6.0.x
      7.0.x
      8.0.x
```

Testing on different OS

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

Generation and publication of documentation

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

Conditional execution of steps

Optimization by skipping unnecessary steps:

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

This workflow provides a complete CI/CD process for a .NET 8 project, including:

* Assembly and testing
* Code quality analysis
* Publication of artifacts
* Deployment to different environments
* Documentation generation
* Conditional execution of steps

You can adapt it to your needs by adding or removing steps depending on the requirements of your project.
These practices will help optimize your GitHub Actions workflows, making them more efficient, reliable, and easier to maintain. It is important to regularly review and update these settings according to the needs of your project.

### Automatic cancellation of GitHub Actions on new commits

When we're actively working on code and making a lot of commits to a branch, it's often the case that several of the same GitHub Actions checks are running at the same time. This can be inefficient, because we are only interested in the result of the last commit.

Why is this necessary?
Automatic cancellation of previous checks provides the following advantages:

* Saves resources if you use paid runners
* Reduces queue time for important tasks
* Prevents free runners from being overloaded (GitHub has limits on simultaneous runs)

How to set it up?
GitHub Actions has a special concurrency option that allows you to group and manage simultaneous executions. 
Here is a simple example:

{% raw %}

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

{% endraw %}

This code creates a group with a unique name for each branch or pull request
Automatically cancels previous runs on a new commit

### Special setting for master branch

Often we want checks in the main branch to not be undone. For this, you can use the following option:

{% raw %}

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref == 'refs/heads/main' && github.run_id || github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

{% endraw %}

Now checks will be canceled only in working branches, and in main all will be executed to the end.

> Useful advice
{: .prompt-info }

Instead of explicitly specifying 'main', you can use github.ref_protected. Then the rule will work for all protected branches automatically.
This setting is especially useful when you:

* Actively working on new functionality
* Make corrections often
* Have limited resources for CI/CD
* Work in a team with many developers

## Conclusion

---

GitHub Actions is a powerful and flexible platform for automating development processes, offering a wide range of opportunities to create an effective CI/CD pipeline. The main advantages of the platform include:

* Tight integration with the GitHub ecosystem
* Ease of configuration through YAML configuration
* A rich selection of ready-made actions from the community
* Support for various operating systems and environments
* Free for open-source projects

Thanks to detailed documentation and an active community, developers can quickly start using GitHub Actions in their projects. The platform is useful for many developers, providing ready-made solutions for:

* Build and test automation
* Deployment of applications
* Documentation generation
* Release management
* Workflow optimization

The use of additional functions, such as caching of dependencies and automatic cancellation of unnecessary workflow launches, allows you to further optimize the development process and effectively use available resources.
GitHub Actions continues to be actively developed, constantly adding new features and improvements, making this platform one of the best solutions for setting up CI/CD processes in modern software development.