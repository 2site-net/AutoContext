# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

## Repository layout

- `src/SharpPilot.Mcp.DotNet/` — MCP server (.NET 10, C#)
- `src/SharpPilot.Mcp.DotNet.Tests/` — xUnit test suite for the .NET MCP server
- `src/SharpPilot.Mcp.Web/` — MCP server (TypeScript tools, Node.js)
- `src/SharpPilot.WorkspaceServer/` — Workspace server (named-pipe, EditorConfig resolution)
- `src/SharpPilot.WorkspaceServer.Tests/` — xUnit test suite for the workspace server
- `src/SharpPilot.VSCode/` — VS Code extension (TypeScript, ESM, Vitest)

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

**Always run `build.ps1` in the foreground (non-background) terminal.** It completes in seconds and does not need a background process. Running it in background mode accumulates orphan terminal sessions that degrade VS Code performance.

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
