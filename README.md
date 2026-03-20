![SharpPilot](small-logo.png)

# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

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

The MCP server exposes three tool scopes. The VS Code extension registers the
server three times — `--scope dotnet`, `--scope git`, and `--scope editorconfig`
— so that .NET, Git, and EditorConfig tools appear in separate sections in the
tools UI.

Servers are workspace-aware: the extension only registers a server when the
workspace contains matching content (e.g., `.csproj` files for .NET, `.git`
directory for Git). If all sub-checks for a scope are disabled in settings, that
server is not registered at all.

### SharpPilot: DotNet

Two tools that analyse C# source and project files for common quality issues.

| Tool | Purpose |
|------|---------|
| `check_dotnet` | Runs all enabled .NET code quality checks on C# source and returns a combined report. Covers coding style, member ordering, naming conventions, async patterns, nullable context, project structure, and test style. When an `.editorconfig` path is provided, resolves its properties and suppresses conflicting checks. |
| `check_nuget_hygiene` | No duplicate, floating, or wildcard package versions; no missing `Version` attribute (unless Central Package Management is enabled); flags packages with built-in .NET alternatives. |

### SharpPilot: Git

One composite tool that validates git commit messages against Conventional
Commits and content best practices.

| Tool | Purpose |
|------|---------|
| `check_git_commit` | Runs all enabled Git commit quality checks on a commit message and returns a combined report. Covers commit format (Conventional Commits) and commit content best practices. |

### SharpPilot: EditorConfig

One tool that resolves effective `.editorconfig` properties for a given file.

| Tool | Purpose |
|------|---------|
| `get_editorconfig` | Resolves the effective `.editorconfig` properties for a given file path — walks the directory tree, evaluates glob patterns and section cascading, and returns the final key-value pairs. |

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
    },
    "editorconfig-SharpPilot": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/SharpPilot/SharpPilot.csproj",
        "--",
        "--scope",
        "editorconfig"
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

