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

The extension ships 13 instruction files that are automatically injected into
GitHub Copilot's context when relevant to your workspace:

| Instruction | Activates when |
|-------------|----------------|
| Copilot Rules | Always |
| Async/Await | .NET project detected |
| Blazor | `.razor` files detected |
| Code Quality | .NET project detected |
| Code Style | .NET project detected |
| Coding Standards | .NET project detected |
| Debugging | .NET project detected |
| Design Principles | .NET project detected |
| NuGet | .NET project detected |
| Performance & Memory | .NET project detected |
| Testing | .NET project detected |
| xUnit | xUnit referenced in a `.csproj` |
| Commit Format | Git repository detected |

The status bar shows how many instructions are currently active (e.g.
`SharpPilot: 13/13`). Click it — or run **SharpPilot: Toggle Instructions** from
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

