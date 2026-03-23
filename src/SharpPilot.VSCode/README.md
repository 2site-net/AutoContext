![SharpPilot](small-logo.png)

# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

## MCP Tools

Once installed, the following tools are available to GitHub Copilot in Agent
mode. Invoke them by asking Copilot to check your code or commits.

Servers are registered only when relevant to the workspace — the .NET and
EditorConfig servers appear only when .NET project files are detected, and the
Git server appears only when a `.git` directory is present.

### .NET

| Tool | What it checks |
|------|----------------|
| `check_dotnet` | Runs all enabled .NET code quality checks on C# source and returns a combined report. Covers coding style, member ordering, naming conventions, async patterns, nullable context, project structure, and test style. When an `.editorconfig` path is provided, resolves its properties and suppresses conflicting checks. |
| `check_nuget_hygiene` | No duplicate, floating, or wildcard package versions; no missing `Version` attribute (unless Central Package Management is enabled); flags packages with built-in .NET alternatives. |

### Git

| Tool | What it checks |
|------|----------------|
| `check_git_commit` | Runs all enabled Git commit quality checks and returns a combined report. Covers commit format (Conventional Commits) and commit content best practices. |

### EditorConfig

| Tool | What it does |
|------|--------------|
| `get_editorconfig` | Resolves the effective `.editorconfig` properties for a given file path — walks the directory tree, evaluates glob patterns and section cascading, and returns the final key-value pairs. |

Each sub-check within `check_dotnet` and `check_git_commit` can be toggled
individually under **Settings → SharpPilot → Tools**, or via
**SharpPilot: Toggle Tools** in the Command Palette. If all sub-checks for a
server are disabled, that server is not registered at all.

## Coding Instructions

The extension ships 57 instruction files that are automatically injected into
GitHub Copilot's context when relevant to your workspace:

### General

| Instruction | Activates when |
|-------------|----------------|
| Copilot Rules | Always |
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
`$(book) 42/57 $(tools) 8/11`). Click it to open a menu where you can toggle
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

## Per-Rule Disable

Individual rules within any instruction file can be disabled without turning off
the entire instruction:

1. Run **SharpPilot: Browse Instructions** and select an instruction.
2. The file opens in a virtual document with a **Disable Rule** / **Enable Rule**
   CodeLens above each rule.
3. Click a CodeLens to toggle the rule. Disabled rules are dimmed and tagged
   `[DISABLED]`.
4. When any rules are disabled, a **Reset All Rules** CodeLens appears at the
   top of the file to re-enable everything at once.

Disabled rules are excluded from the instructions that Copilot receives. The
disable state is stored in `.sharppilot.json` in your workspace root — commit it
for team-wide settings or add it to `.gitignore` for personal preferences.

> **Note:** Disabled rules are tracked by a content hash. If an extension update
> changes the wording of a rule, the old hash no longer matches and the rule is
> silently re-enabled.

## Commands

| Command | Description |
|---------|-------------|
| **SharpPilot: Toggle Instructions** | Enable or disable coding instructions. |
| **SharpPilot: Toggle Tools** | Enable or disable individual tool checks. |
| **SharpPilot: Auto Configure** | Scan the workspace and enable relevant items. |
| **SharpPilot: Export Instructions** | Export instruction files to `.github/instructions/`. |
| **SharpPilot: Browse Instructions** | Preview an instruction file with per-rule disable/enable CodeLens. |
| **SharpPilot: Toggle Rule** | Disable or re-enable a single rule (invoked via CodeLens). |
| **SharpPilot: Reset All Rules** | Re-enable all disabled rules for the current file (invoked via CodeLens). |

## Prerequisites

- VS Code 1.99 or later with [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot).

No .NET runtime is required — the extension ships self-contained executables.

## Installation

Install the platform-specific `.vsix` for your OS from the Extensions view
(**Install from VSIX…**) or from the command line:

```sh
code --install-extension SharpPilot-win32-x64-0.1.0.vsix
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

