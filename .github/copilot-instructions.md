# AutoContext

AutoContext is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

## Coding Guidelines

Detailed instruction files live in `src/AutoContext.VSCode/instructions/`.
**Do** read the relevant file **before** generating or reviewing code:

| When working on          | Read                                                                                                                          |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| Any code                 | `copilot.instructions.md`, `design-principles.instructions.md`                                                                |
| C# source               | `lang-csharp.instructions.md`, `dotnet-coding-standards.instructions.md`, `dotnet-async-await.instructions.md`                 |
| C# tests                | `testing.instructions.md`, `dotnet-testing.instructions.md`, `dotnet-xunit.instructions.md`                                   |
| NuGet / .csproj          | `dotnet-nuget.instructions.md`                                                                                                |
| TypeScript / VS Code ext | `lang-typescript.instructions.md`                                                                                             |
| TypeScript tests         | `testing.instructions.md`, `web-testing.instructions.md`, `web-vitest.instructions.md`                                        |
| Git commits              | `git-commit-format.instructions.md`                                                                                           |
| PowerShell scripts       | `lang-powershell.instructions.md`                                                                                             |
| Bash / shell scripts     | `lang-bash.instructions.md`                                                                                                   |
| Batch (CMD) scripts      | `lang-batch.instructions.md`                                                                                                  |
| REST API design          | `rest-api-design.instructions.md`                                                                                             |

All paths are relative to `src/AutoContext.VSCode/instructions/`.

## Context

- **Do** read `README.md` for repository structure, build prerequisites, and manual server configuration.
- **Do** read `docs/architecture.md` for the activation flow, runtime flow, MCP server modes, and precedence rules.
- **Don't** duplicate information from these files — they are the single source of truth.

## Build & Test

- **Do** always run `build.ps1` in the foreground (non-background) terminal — it completes in seconds and does not need a background process.
- **Don't** run `build.ps1` in background mode — it accumulates orphan terminal sessions that degrade VS Code performance.

```powershell
# Show all available actions, targets, and switches
./build.ps1 -Help

# Everything (compile + test)
./build.ps1

# TypeScript only
./build.ps1 Compile TS
./build.ps1 Test TS

# .NET only
./build.ps1 Compile DotNet
./build.ps1 Test DotNet

# Package for current platform
./build.ps1 Package
```
