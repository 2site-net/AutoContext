# QaMcp

A collection of MCP servers that give AI coding assistants — such as GitHub
Copilot — the ability to enforce project quality standards in real time.

## Servers

The extension registers two MCP servers that run locally via **stdio**.

### DotNet QA MCP

Eight tools that analyse C# source and project files for common quality issues.

| Tool | Purpose |
|------|---------|
| `check_code_style` | No `#region`, no decorative comments, curly braces on control flow (except single-line guard clauses), blank lines before control flow, expression-body arrows on the next line, and XML doc on public/protected members. |
| `check_member_ordering` | Members must follow constants → static fields → fields → constructors → delegates → events → enums → properties → indexers → methods → operators → nested types, then public → private, static before instance, then alphabetically. Test classes are skipped. |
| `check_naming_conventions` | `I`-prefixed interfaces, `Extensions`-suffixed extension classes, `Async`-suffixed async methods, `_camelCase` private fields, PascalCase types/methods/properties/events, and camelCase parameters. |
| `check_async_patterns` | No async void (except event handlers), `CancellationToken` required on public async methods, `.ConfigureAwait(false)` required on all await expressions in non-test code. |
| `check_nullable_context` | `#nullable disable` and the null-forgiving operator (`!`) are not allowed. |
| `check_project_structure` | File-scoped namespaces required, one type per file, file name must match type name, and `#pragma warning disable` is not allowed. |
| `check_nuget_hygiene` | No duplicate, floating, or wildcard package versions; no missing `Version` attribute (unless Central Package Management is enabled); flags packages with built-in .NET alternatives. |
| `check_tests_style` | Test classes suffixed `Tests`, methods prefixed `Should_`/`Should_not_`, no XML doc on tests, `Assert.Multiple()` when multiple asserts, no `.ConfigureAwait()` in tests, and optional file-structure mirroring validation. |

### Git QA MCP

Two tools that validate git commit messages against Conventional Commits and
content best practices.

| Tool | Purpose |
|------|---------|
| `validate_commit_format` | Subject must follow `type(scope): description`, stay under 50 characters, body wrapped at 72 characters, blank line between subject and body. |
| `validate_commit_content` | No bullet lists, file paths, counts, enumerated properties, "Key features:" sections, or sensitive information in the commit body. |

## Prerequisites

- VS Code 1.99 or later with [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot).

No .NET runtime is required — the extension ships self-contained executables.

## Installation

Install the platform-specific `.vsix` for your OS from the Extensions view
(**Install from VSIX…**) or from the command line:

```sh
code --install-extension qa-mcp-win32-x64-0.1.0.vsix
```

Once installed, open Agent mode in Copilot Chat and the QA MCP tools will
appear in the tools picker.

## Building from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and Node.js 18+.

```sh
npm install
# Package for a specific platform:
npm run package:win-x64
npm run package:osx-arm64
npm run package:linux-x64
# Or package all platforms:
npm run package:all
```

Available targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-x64`, `osx-arm64`.
