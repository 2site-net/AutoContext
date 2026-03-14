# QaMcp

A collection of MCP servers that give GitHub Copilot the ability to validate
C# code quality and Git commit conventions. The servers are distributed as a
VS Code extension — see [src/VSCodeQaMcp/README.md](src/VSCodeQaMcp/README.md) for
end-user installation and usage.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 18+ — required to build the VS Code extension

## Repository Structure

```text
QaMcp.slnx                  # Solution file
src/QaMcp/                  # MCP server (.NET + Git tools)
tests/QaMcp.Tests/          # xUnit tests
src/VSCodeQaMcp/            # VS Code extension that registers the server
```

## Servers and Tools

The MCP server exposes two tool scopes. The VS Code extension registers the
server twice — once with `--scope dotnet` and once with `--scope git` — so
that .NET and Git tools appear in separate sections in the tools UI.

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

## Manual Server Configuration

If you have the .NET 10 SDK installed and have cloned this repo, you can
register the servers directly in `.vscode/mcp.json` without the VS Code
extension. This is useful for development or for using the latest unreleased
server code:

```jsonc
{
  "servers": {
    "dotnet-qa-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/QaMcp/QaMcp.csproj",
        "--",
        "--scope",
        "dotnet"
      ]
    },
    "git-qa-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/QaMcp/QaMcp.csproj",
        "--",
        "--scope",
        "git"
      ]
    }
  }
}
```

## Building and Publishing the Extension

Package a platform-specific VSIX:

```sh
cd src/VSCodeQaMcp
npm install
npm run package:win-x64      # or osx-arm64, linux-x64, etc.
npm run package:all           # all platforms
```

Available targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-x64`, `osx-arm64`.

Publish to the VS Code Marketplace (requires a
[Personal Access Token](https://dev.azure.com/_usersSettings/tokens) with
**Marketplace → Manage** scope):

```sh
cd src/VSCodeQaMcp
npx vsce login <publisher-id>
npm run publish:all
```

## License

[MIT](src/VSCodeQaMcp/LICENSE)

