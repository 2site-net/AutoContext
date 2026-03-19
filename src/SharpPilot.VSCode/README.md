# SharpPilot

SharpPilot is a development quality toolkit for GitHub Copilot, providing a curated set of tools and guidelines to improve code quality and the overall development workflow.

## MCP Tools

Once installed, the following tools are available to GitHub Copilot in Agent
mode. Invoke them by asking Copilot to check your code or commits.

### .NET

| Tool | What it checks |
|------|----------------|
| `check_csharp_coding_style` | No `#region`, no decorative comments, curly braces on control flow (except single-line guard clauses), blank lines before control flow, expression-body arrows on the next line, and XML doc on public/protected members. |
| `check_csharp_member_ordering` | Members must follow constants → static fields → fields → constructors → delegates → events → enums → properties → indexers → methods → operators → nested types, then public → private, static before instance, then alphabetically. Test classes are skipped. |
| `check_csharp_naming_conventions` | `I`-prefixed interfaces, `Extensions`-suffixed extension classes, `Async`-suffixed async methods, `_camelCase` private fields, PascalCase types/methods/properties/events, and camelCase parameters. |
| `check_csharp_async_patterns` | No async void (except event handlers), `CancellationToken` required on public async methods, `.ConfigureAwait(false)` required on all await expressions in non-test code. |
| `check_csharp_nullable_context` | `#nullable disable` and the null-forgiving operator (`!`) are not allowed. |
| `check_csharp_project_structure` | File-scoped namespaces required, one type per file, file name must match type name, and `#pragma warning disable` is not allowed. |
| `check_nuget_hygiene` | No duplicate, floating, or wildcard package versions; no missing `Version` attribute (unless Central Package Management is enabled); flags packages with built-in .NET alternatives. |
| `check_csharp_test_style` | Test classes suffixed `Tests`, methods prefixed `Should_`/`Should_not_`, no XML doc on tests, `Assert.Multiple()` when multiple asserts, no `.ConfigureAwait()` in tests, and optional file-structure mirroring validation. |

### Git

| Tool | What it checks |
|------|----------------|
| `check_git_commit_format` | Subject must follow `type(scope): description`, stay under 50 characters, body wrapped at 72 characters, blank line between subject and body. |
| `check_git_commit_content` | No bullet lists, file paths, counts, enumerated properties, "Key features:" sections, or sensitive information in the commit body. |

## Coding Instructions

The extension ships 56 instruction files that are automatically injected into
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
| Vue.js | Vue detected in `package.json` |
| Testing | Any web test framework detected |
| Vitest | Vitest detected in `package.json` |
| Jest | Jest detected in `package.json` |
| Jasmine | Jasmine detected in `package.json` |
| Mocha | Mocha detected in `package.json` |
| Playwright | Playwright detected in `package.json` |
| Cypress | Cypress detected in `package.json` |

### Web Languages

| Instruction | Activates when |
|-------------|----------------|
| CSS | `.css` files detected |
| HTML | `.html` or `.cshtml` files detected |
| JavaScript | `.js` or `.ts` files detected |
| TypeScript | `.ts` files detected |

The status bar shows how many instructions are currently active (e.g.
`SharpPilot: 42/56`). Click it — or run **SharpPilot: Toggle Instructions** from
the Command Palette — to enable or disable individual instructions without
opening Settings.

Each instruction can also be toggled individually under
**Settings → SharpPilot → Instructions**.

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

