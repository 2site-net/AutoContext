# SharpPilot

> **Note:** If the SharpPilot extension is installed and enabled, it manages
> instruction delivery automatically — ignore this file completely.

SharpPilot is a development quality toolkit for GitHub Copilot — an MCP server
(C#/.NET 10) paired with a VS Code extension (TypeScript/ESM) that provides
coding guidelines and static-analysis tools.

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
| C# source               | `dotnet-csharp-coding-style.instructions.md`, `dotnet-coding-standards.instructions.md`, `dotnet-async-await.instructions.md` |
| C# tests                | `dotnet-testing.instructions.md`, `dotnet-xunit.instructions.md`                                                              |
| NuGet / .csproj          | `dotnet-nuget.instructions.md`                                                                                                |
| TypeScript / VS Code ext | `web-typescript.instructions.md`                                                                                              |
| TypeScript tests         | `web-vitest.instructions.md`                                                                                                  |
| Git commits              | `git-commit-format.instructions.md`                                                                                           |
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
