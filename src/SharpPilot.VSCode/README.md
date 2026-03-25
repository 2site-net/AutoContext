![SharpPilot](small-logo.png)

# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

## How It Works

SharpPilot operates as a deterministic, multi-layer pipeline. Each layer feeds
workspace-specific context into the next so that Copilot receives exactly the
right tools, instructions, and enforcement rules for your project.

### 1. Workspace detection

On activation the extension scans your workspace for project files
(`.csproj`, `.fsproj`, `package.json`, …), directories (`.git`), and
dependencies. Each finding sets a boolean context key (e.g., `hasDotnet`,
`hasGit`, `hasTypeScript`) that the remaining layers consume.

### 2. Server registration

One MCP server is registered per scope — **DotNet**, **Git**, and
**EditorConfig**. A server is only registered when:

- Its context key is true (the workspace contains matching content).
- At least one of its tools is enabled in settings.

If either condition is not met the server does not appear at all.

### 3. Instruction injection

60 instruction files are conditionally injected into Copilot's context based on
the same context keys (e.g., "ASP.NET Core" instructions only appear when an
ASP.NET Core project is detected). Individual rules inside any instruction file
can be disabled via `.sharppilot.json` without turning off the entire file.

### 4. Tool configuration

VS Code settings control which sub-checks are enabled. The extension writes
disabled tool names to `.sharppilot.json` in the workspace root. The MCP server
reads this file at runtime and skips disabled sub-checks.

### 5. Runtime — EditorConfig-driven enforcement

When Copilot invokes `check_dotnet`, the server resolves the project's
`.editorconfig` properties and uses them to **drive** checker behavior. For
example, if `csharp_prefer_braces = false`, the brace checker flags
*unnecessary* braces instead of *missing* ones. EditorConfig values determine
the enforcement direction — checkers don't just skip conflicting rules, they
enforce whichever direction the project's EditorConfig specifies.

### Precedence

EditorConfig → instruction files → VS Code settings → workspace context.
See the [full precedence table](https://github.com/2site-net/SharpPilot#precedence) in the repository
README for details.

## MCP Tools

Once installed, the following tools are available to GitHub Copilot in Agent
mode. Invoke them by asking Copilot to check your code or commits.

| Scope | Tool | Purpose |
|-------|------|---------|
| .NET | `check_dotnet` | Composite C# quality check (style, naming, async, structure, …) |
| .NET | `check_nuget_hygiene` | Package version and hygiene check |
| Git | `check_git_commit` | Conventional Commits format and content check |
| EditorConfig | `get_editorconfig` | Resolve effective `.editorconfig` properties for a file |

See the [Servers and Tools](https://github.com/2site-net/SharpPilot#servers-and-tools) section in the
repository README for full tool descriptions.

Each sub-check within `check_dotnet` and `check_git_commit` can be toggled
individually under **Settings → SharpPilot → Tools**, or via
**SharpPilot: Toggle Tools** in the Command Palette. If all sub-checks for a
server are disabled, that server is not registered at all.

## Coding Instructions

The extension ships 60 instruction files that are automatically injected into
GitHub Copilot's context when relevant to your workspace:

### General

| Instruction | Activates when |
|-------------|----------------|
| Copilot Instructions | Always |
| Code Review | Always |
| Design Principles | Always |
| Docker | Dockerfile detected |
| GraphQL | GraphQL package detected |
| REST API Design | Always |
| SQL | Always |

### .NET

| Instruction | Activates when |
|-------------|----------------|
| ASP.NET Core | ASP.NET Core project detected |
| Async/Await | .NET project detected |
| Blazor | `.razor` files detected |
| Coding Standards | .NET project detected |
| Core (DI, Logging, Config, Security) | .NET project detected |
| Dapper | Dapper package detected |
| Debugging | .NET project detected |
| Entity Framework Core | EF Core package detected |
| gRPC | gRPC package detected |
| .NET MAUI | MAUI project detected |
| Mediator / CQRS | MediatR package detected |
| MongoDB | MongoDB driver detected |
| MySQL | MySQL package detected |
| NuGet | .NET project detected |
| Oracle | Oracle package detected |
| Performance & Memory | .NET project detected |
| PostgreSQL | Npgsql package detected |
| Razor | `.razor` files detected |
| Redis | StackExchange.Redis detected |
| SignalR | SignalR package detected |
| SQLite | SQLite package detected |
| SQL Server | SqlClient package detected |
| Testing | .NET project detected |
| Unity | Unity project detected |
| Windows Forms | WinForms project detected |
| WPF | WPF project detected |
| xUnit | xUnit package detected |
| MSTest | MSTest package detected |
| NUnit | NUnit package detected |

### .NET Languages

| Instruction | Activates when |
|-------------|----------------|
| C# Coding Style | .NET project detected |
| F# Coding Style | F# project detected |
| VB.NET Coding Style | VB.NET project detected |

### Git

| Instruction | Activates when |
|-------------|----------------|
| Commit Format | Git repository detected |

### Scripting

| Instruction | Activates when |
|-------------|----------------|
| PowerShell | `.ps1`, `.psm1`, or `.psd1` files detected |
| Bash | `.sh` or `.bash` files detected |
| Batch (CMD) | `.bat` or `.cmd` files detected |

### Web

| Instruction | Activates when |
|-------------|----------------|
| Angular | Angular detected in `package.json` |
| Next.js | Next.js detected in `package.json` |
| Node.js | `package.json` detected |
| React | React detected in `package.json` |
| Svelte | Svelte detected in `package.json` |
| TypeScript | `.ts` files detected |
| Vue.js | Vue detected in `package.json` |
| CSS | `.css` files detected |
| HTML | `.html` or `.cshtml` files detected |
| JavaScript | `.js` or `.ts` files detected |

### Web Testing

| Instruction | Activates when |
|-------------|----------------|
| Testing | Any web test framework detected |
| Vitest | Vitest detected in `package.json` |
| Jest | Jest detected in `package.json` |
| Jasmine | Jasmine detected in `package.json` |
| Mocha | Mocha detected in `package.json` |
| Playwright | Playwright detected in `package.json` |
| Cypress | Cypress detected in `package.json` |

## Status Bar

The status bar shows how many instructions and tools are currently active (e.g.
`$(book) 42/60 $(tools) 8/11`). Click it to open a menu where you can toggle
instructions, toggle tools, auto-configure, export, or browse instructions.

## Auto Configuration

Run **SharpPilot: Auto Configure** from the Command Palette or the status bar
menu. The extension scans your workspace for `.csproj`, `.fsproj`, `.vbproj`,
`package.json`, `.git`, NuGet packages, and npm dependencies, then enables only
the instructions and tools relevant to your project.

## Export & Version Tracking

**SharpPilot: Export Instructions** copies instruction files to
`.github/instructions/` for team sharing. A manifest (`.manifest.json`) tracks
exported file hashes — when instructions are updated in a new release, the
extension alerts you so you can re-export.

## Override Detection

When instruction files exist in `.github/instructions/` or a
`.github/copilot-instructions.md` file is present, SharpPilot detects them as
overrides. Overridden instructions appear with a badge in the toggle menu,
signaling that a local version is in use.

## Per-Instruction Disable

Individual instructions within any instruction file can be disabled without turning off
the entire instruction:

1. Run **SharpPilot: Browse Instructions** and select an instruction.
2. The file opens in a virtual document with a **Disable Instruction** / **Enable Instruction**
   CodeLens above each instruction.
3. Click a CodeLens to toggle the instruction. Disabled instructions are dimmed and tagged
   `[DISABLED]`.
4. When any instructions are disabled, a **Reset All Instructions** CodeLens appears at the
   top of the file to re-enable everything at once.

Disabled instructions are excluded from the instructions that Copilot receives. The
disable state is stored in `.sharppilot.json` in your workspace root — commit it
for team-wide settings or add it to `.gitignore` for personal preferences.

> **Note:** Disabled instructions are tracked by a content hash. If an extension update
> changes the wording of an instruction, the old hash no longer matches and the instruction is
> silently re-enabled.

## Commands

| Command | Description |
|---------|-------------|
| **SharpPilot: Toggle Instructions** | Enable or disable coding instructions. |
| **SharpPilot: Toggle Tools** | Enable or disable individual tool checks. |
| **SharpPilot: Auto Configure** | Scan the workspace and enable relevant items. |
| **SharpPilot: Export Instructions** | Export instruction files to `.github/instructions/`. |
| **SharpPilot: Browse Instructions** | Preview an instruction file with per-instruction disable/enable CodeLens. |
| **SharpPilot: Toggle Instruction** | Disable or re-enable a single instruction (invoked via CodeLens). |
| **SharpPilot: Reset All Instructions** | Re-enable all disabled instructions for the current file (invoked via CodeLens). |

## Prerequisites

- VS Code 1.100 or later with [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot).

No .NET runtime is required — the extension ships self-contained executables.

## Installation

Install the platform-specific `.vsix` for your OS from the Extensions view
(**Install from VSIX…**) or from the command line:

```sh
code --install-extension SharpPilot-win32-x64-0.5.0.vsix
```

Once installed, open Agent mode in Copilot Chat and the SharpPilot tools will
appear in the tools picker. Ask Copilot things like:

- *"Check this file for code style issues."*
- *"Validate my commit message against Conventional Commits."*
- *"Check for async pattern violations in the current file."*

You can verify the servers are running via the Command Palette →
**MCP: List Servers**.

## Source

The source code and contribution guidelines are available at
[github.com/2site-net/SharpPilot](https://github.com/2site-net/SharpPilot).

