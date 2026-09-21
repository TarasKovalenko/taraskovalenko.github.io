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

GitHub is the world's largest platform for collaborative software development, with tools for version control, project management and teamwork. It's built on Git, a version control system that lets teams work on code together and track changes.
GitHub Actions is the platform's built-in tool for automating development, from checking code to deploying it. It takes routine work off the team's plate and helps keep code quality and development speed up. Below are the basics of GitHub Actions, why they're worth using, and a few features almost every project needs but not everyone knows about.

## Why use Github Actions?

---

The biggest advantage of GitHub Actions is that it's part of GitHub. Actions run on events that happen in your repository, so automating things like running tests or deploying code to a staging environment is straightforward.

A new build, test or deployment process takes just a few lines of YAML. And a test matrix automatically checks your code on different operating systems and language versions, so you see compatibility issues right away.

For public repositories, runtime and compute resources are free, so open-source projects and individual developers get full CI/CD at no extra cost.

## Core components of GitHub Actions

---

Here's what GitHub Actions is made of:

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



## Creation of the first Workflow for .NET 8

---

We'll start with a basic workflow for a typical .NET 8 project.

Basic workflow structure

Create the file `.github/workflows/dotnet.yml`:

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

This workflow runs when you open a pull request against `main` or merge into it: it builds your project and runs the tests.

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

Generating and publishing documentation

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

Together these steps give you a complete CI/CD cycle for a .NET 8 project: build and test, code quality analysis, artifact publishing, deployment to different environments, documentation generation, and conditional execution of steps.

Adapt it to your project by adding or removing steps, and revisit these settings from time to time as the project's needs change.

### Automatic cancellation of GitHub Actions on new commits

When you're actively working on code and pushing a lot of commits to a branch, you often end up with several identical GitHub Actions checks running at once. That's wasteful, since you only care about the result of the last commit.

Why do this?
Automatically canceling previous checks:

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

This code creates a group with a unique name for each branch or pull request and automatically cancels previous runs when a new commit arrives.

### Special setting for master branch

Usually you don't want checks on the main branch to be canceled. For that, you can use this variant:

{% raw %}

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref == 'refs/heads/main' && github.run_id || github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

{% endraw %}

Now checks get canceled only in working branches, while in main every run completes.

> Useful advice
{: .prompt-info }

Instead of explicitly specifying 'main', you can use github.ref_protected. Then the rule will work for all protected branches automatically.
This setting is especially useful when you:

* Are actively working on new functionality
* Make corrections often
* Have limited resources for CI/CD
* Work in a team with many developers

## Conclusion

---

GitHub Actions is convenient mainly because it's built into GitHub. You configure it with plain YAML, it runs on different operating systems and environments, it's free for open source, and the community offers a big selection of ready-made actions. That covers build and test automation, application deployment, documentation generation and release management.

Dependency caching and automatic cancellation of redundant workflow runs save additional time and resources, so don't skip them. And since the platform is actively developed, it's worth checking now and then what's new.