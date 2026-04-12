![AutoContext](logo.png)

# AutoContext

AutoContext is a context toolkit for AI coding assistants. It ships with curated instructions that shape how code is written and reviewed, bundled MCP tools that validate code against concrete rules, and a context orchestration layer that automatically wires the right guidance and checks into the model based on the workspace and environment. Today AutoContext is integrated with GitHub Copilot, but its architecture is designed to support additional coding assistants and external context sources over time.

> **Work in Progress** — Instructions and tools are refined iteratively. Coverage, rules, and tool behavior will continue to evolve as we incorporate feedback and expand language and framework support.

Distributed as a VS Code extension — see [src/AutoContext.VSCode/README.md](src/AutoContext.VSCode/README.md) for installation and usage.

## Features

AutoContext provides two complementary capabilities:

- **Chat Instructions** — Curated Markdown guidelines covering .NET, TypeScript, Web frameworks, Git, scripting, and more. Instructions are workspace-aware — only the ones relevant to your project are injected into Copilot's context. Individual rules within any instruction file can be disabled without turning off the entire file.
- **MCP Tool Checks** — Quality checks that Copilot can invoke in Agent mode to validate code style, naming conventions, async patterns, NuGet hygiene, commit messages, and more. Checkers read `.editorconfig` properties and enforce whichever direction the project specifies.

Tools and instructions are grouped into categories and managed from dedicated sidebar panels — individually toggled, auto-configured based on workspace detection, or exported to `.github/instructions/` for team sharing.

## Build Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 18+ — required to build the VS Code extension

## Repository Structure

```text
AutoContext.slnx                        # Solution file
src/AutoContext.Mcp.Shared/             # Shared contracts and communication layer for .NET MCP servers
src/AutoContext.WorkspaceServer/        # Handles cross-cutting workspace tasks and hosts technology-agnostic MCP tools
src/AutoContext.Mcp.DotNet/             # Provides MCP tools server for .NET development (e.g. C#, NuGet)
src/AutoContext.Mcp.Web/                # Provides MCP tools server for web development (e.g. TypeScript)
src/AutoContext.VSCode/                 # VS Code extension for instructions, tool orchestration, and workspace detection
src/AutoContext.Mcp.DotNet.Tests/       # Tests for the .NET MCP server
src/AutoContext.WorkspaceServer.Tests/  # Tests for the workspace server
```

## Architecture

See [docs/architecture.md](docs/architecture.md) for the full architecture guide — layer pipeline, activation and runtime flows, precedence rules, MCP servers, and tool reference.

## Manual Server Configuration

If you have the .NET 10 SDK installed and have cloned this repo, you can register the servers directly in `.vscode/mcp.json` without the VS Code extension. This is useful for development or for using the latest unreleased server code:

```jsonc
{
  "servers": {
    "dotnet-AutoContext": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/AutoContext.Mcp.DotNet/AutoContext.Mcp.DotNet.csproj",
        "--",
        "--scope",
        "dotnet"
      ]
    },
    "git-AutoContext": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/AutoContext.WorkspaceServer/AutoContext.WorkspaceServer.csproj",
        "--",
        "--scope",
        "git"
      ]
    },
    "editorconfig-AutoContext": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/AutoContext.WorkspaceServer/AutoContext.WorkspaceServer.csproj",
        "--",
        "--scope",
        "editorconfig"
      ]
    },
    "typescript-AutoContext": {
      "type": "stdio",
      "command": "node",
      "args": [
        "${workspaceFolder}/src/AutoContext.Mcp.Web/out/index.js",
        "--scope",
        "typescript"
      ]
    }
  }
}
```

> **Note:** The TypeScript server requires a prior build: `cd src/AutoContext.Mcp.Web && npm install && npm run build`. When configured manually, EditorConfig properties are not resolved (the `--workspace-server` argument is omitted); checkers fall back to their built-in defaults.

## Testing

```powershell
./build.ps1                   # compile + test (all)
./build.ps1 Test TS           # TypeScript tests only
./build.ps1 Test DotNet       # .NET tests only
```

### VS Code Extension — Smoke Tests

Smoke tests launch a real VS Code instance, load the extension, and verify activation and command registration:

```sh
cd src/AutoContext.VSCode
npm install
npm run test:smoke
```

A VS Code installation is downloaded automatically on the first run and cached
in `src/AutoContext.VSCode/.vscode-test/`.

## Building and Publishing the Extension

```powershell
./build.ps1 Package                          # current platform
./build.ps1 Package All                      # all 6 platforms
./build.ps1 Package -RuntimeIdentifier win-x64
```

Available targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

Publish to the VS Code Marketplace (requires a [Personal Access Token](https://dev.azure.com/_usersSettings/tokens) with **Marketplace → Manage** scope):

```powershell
./build.ps1 Publish           # current platform
./build.ps1 Publish All       # all 6 platforms
```

## License

AutoContext is licensed under the [AGPL-3.0](LICENSE). A separate [commercial license](COMMERCIAL.md) is available for organizations that want to use AutoContext under terms different from the AGPL-3.0.

Use of the AutoContext name and logo is subject to [TRADEMARKS.md](TRADEMARKS.md).

## Contributing

Contributions require acceptance of the [Contributor License Agreement](CLA.md). See [CONTRIBUTING.md](CONTRIBUTING.md) for how to get started.
