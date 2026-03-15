# SharpPilot

SharpPilot is a quality assurance toolkit for GitHub Copilot, providing tools and coding guidelines that improve code quality and the overall development workflow. 

Distributed as a VS Code extension — see [src/SharpPilot.VSCode/README.md](src/SharpPilot.VSCode/README.md) for installation and usage.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 18+ — required to build the VS Code extension

## Repository Structure

```text
SharpPilot.slnx                  # Solution file
src/SharpPilot/                  # MCP server (.NET + Git tools)
tests/SharpPilot.Tests/          # xUnit tests
src/SharpPilot.VSCode/            # VS Code extension that registers the server
```

## Servers and Tools

The MCP server exposes two tool scopes. The VS Code extension registers the
server twice — once with `--scope dotnet` and once with `--scope git` — so
that .NET and Git tools appear in separate sections in the tools UI.

### SharpPilot: DotNet

Eight tools that analyse C# source and project files for common quality issues.

| Tool | Purpose |
|------|---------|
| `check_csharp_coding_style` | No `#region`, no decorative comments, curly braces on control flow (except single-line guard clauses), blank lines before control flow, expression-body arrows on the next line, and XML doc on public/protected members. |
| `check_csharp_member_ordering` | Members must follow constants → static fields → fields → constructors → delegates → events → enums → properties → indexers → methods → operators → nested types, then public → private, static before instance, then alphabetically. Test classes are skipped. |
| `check_csharp_naming_conventions` | `I`-prefixed interfaces, `Extensions`-suffixed extension classes, `Async`-suffixed async methods, `_camelCase` private fields, PascalCase types/methods/properties/events, and camelCase parameters. |
| `check_csharp_async_patterns` | No async void (except event handlers), `CancellationToken` required on public async methods, `.ConfigureAwait(false)` required on all await expressions in non-test code. |
| `check_csharp_nullable_context` | `#nullable disable` and the null-forgiving operator (`!`) are not allowed. |
| `check_csharp_project_structure` | File-scoped namespaces required, one type per file, file name must match type name, and `#pragma warning disable` is not allowed. |
| `check_nuget_hygiene` | No duplicate, floating, or wildcard package versions; no missing `Version` attribute (unless Central Package Management is enabled); flags packages with built-in .NET alternatives. |
| `check_csharp_test_style` | Test classes suffixed `Tests`, methods prefixed `Should_`/`Should_not_`, no XML doc on tests, `Assert.Multiple()` when multiple asserts, no `.ConfigureAwait()` in tests, and optional file-structure mirroring validation. |

### SharpPilot: Git

Two tools that validate git commit messages against Conventional Commits and
content best practices.

| Tool | Purpose |
|------|---------|
| `check_git_commit_format` | Subject must follow `type(scope): description`, stay under 50 characters, body wrapped at 72 characters, blank line between subject and body. |
| `check_git_commit_content` | No bullet lists, file paths, counts, enumerated properties, "Key features:" sections, or sensitive information in the commit body. |

## Manual Server Configuration

If you have the .NET 10 SDK installed and have cloned this repo, you can
register the servers directly in `.vscode/mcp.json` without the VS Code
extension. This is useful for development or for using the latest unreleased
server code:

```jsonc
{
  "servers": {
    "dotnet-SharpPilot": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/SharpPilot/SharpPilot.csproj",
        "--",
        "--scope",
        "dotnet"
      ]
    },
    "git-SharpPilot": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/SharpPilot/SharpPilot.csproj",
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
cd src/SharpPilot.VSCode
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
cd src/SharpPilot.VSCode
npx vsce login <publisher-id>
npm run publish:all
```

## License

[Apache-2.0](src/SharpPilot.VSCode/LICENSE)

