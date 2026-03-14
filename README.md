# QaMcp

A collection of MCP servers that give AI coding assistants — such as GitHub
Copilot — the ability to enforce project quality standards in real time.

## Repository Structure

```text
QaMcp.slnx                  # Solution file
DotNetQaMcp/                # C# source-analysis MCP server
  src/DotNetQaMcp/
  tests/DotNetQaMcp.Tests/
GitQaMcp/                   # Git commit-validation MCP server
  src/GitQaMcp/
  tests/GitQaMcp.Tests/
VSCodeQaMcp/                # VS Code extension that registers both servers
```

## Servers and Tools

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

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or later) — required
  for building from source and for the manual `.vscode/mcp.json` workflow.
- [Node.js](https://nodejs.org/) 18+ — required for building the VS Code
  extension.
- Extension users do **not** need .NET or Node.js installed — the VSIX ships
  self-contained executables.

## Build

```sh
dotnet build
```

## Test

```sh
dotnet test
```

## Usage

### VS Code Extension (no .NET required)

The `VSCodeQaMcp/` directory contains a VS Code extension that bundles both
servers as self-contained executables — consumers do not need .NET installed.

Package a platform-specific VSIX:

```sh
cd VSCodeQaMcp
npm install
npm run package:win-x64      # or osx-arm64, linux-x64, etc.
npm run package:all           # all platforms
```

Available targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-x64`, `osx-arm64`.

Install the VSIX locally:

```sh
code --install-extension VSCodeQaMcp/qa-mcp-win32-x64-0.1.0.vsix
```

#### Publishing to the Marketplace

Login once (requires a
[Personal Access Token](https://dev.azure.com/_usersSettings/tokens) with
**Marketplace → Manage** scope):

```sh
cd VSCodeQaMcp
npx vsce login <publisher-id>
```

Build all platform packages and publish them:

```sh
npm run publish:all
```

See [VSCodeQaMcp/README.md](VSCodeQaMcp/README.md) for more details.

### Manual Configuration (requires .NET SDK)

If you have the .NET SDK installed, you can register the servers directly in
`.vscode/mcp.json` without the VS Code extension. Replace
`${workspaceFolder}` with the absolute path to the cloned repo if needed:

```jsonc
{
  "servers": {
    "dotnet-qa-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/DotNetQaMcp/src/DotNetQaMcp/DotNetQaMcp.csproj"
      ]
    },
    "git-qa-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/GitQaMcp/src/GitQaMcp/GitQaMcp.csproj"
      ]
    }
  }
}
```

## License

[MIT](VSCodeQaMcp/LICENSE)
