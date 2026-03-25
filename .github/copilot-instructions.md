# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

## Repository layout

- `src/SharpPilot/` — MCP server (.NET 10, C#)
- `src/SharpPilot.VSCode/` — VS Code extension (TypeScript, ESM, Vitest)
- `tests/SharpPilot.Tests/` — xUnit test suite for the MCP server

## Coding guidelines

Detailed instruction files live in `src/SharpPilot.VSCode/instructions/`.
Read the relevant file **before** generating or reviewing code:

| When working on          | Read                                                                                                                          |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| Any code                 | `copilot.instructions.md`, `design-principles.instructions.md`                                                                |
| C# source               | `dotnet-csharp.instructions.md`, `dotnet-coding-standards.instructions.md`, `dotnet-async-await.instructions.md` |
| C# tests                | `dotnet-testing.instructions.md`, `dotnet-xunit.instructions.md`                                                              |
| NuGet / .csproj          | `dotnet-nuget.instructions.md`                                                                                                |
| TypeScript / VS Code ext | `web-typescript.instructions.md`                                                                                              |
| TypeScript tests         | `web-vitest.instructions.md`                                                                                                  |
| Git commits              | `git-commit-format.instructions.md`                                                                                           |
| PowerShell scripts       | `scripting-powershell.instructions.md`                                                                                        |
| Bash / shell scripts     | `scripting-bash.instructions.md`                                                                                              |
| Batch (CMD) scripts      | `scripting-batch.instructions.md`                                                                                             |
| REST API design          | `rest-api-design.instructions.md`                                                                                             |

All paths are relative to `src/SharpPilot.VSCode/instructions/`.

## Build & test

```powershell
# MCP server
dotnet build src/SharpPilot/SharpPilot.csproj
dotnet test  tests/SharpPilot.Tests/SharpPilot.Tests.csproj

# VS Code extension
cd src/SharpPilot.VSCode && npm test
```
