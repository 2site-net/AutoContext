![SharpPilot](small-logo.png)

# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

Distributed as a VS Code extension — see [src/SharpPilot.VSCode/README.md](src/SharpPilot.VSCode/README.md) for installation and usage.

## Features

- **60 Chat Instructions** — Curated Markdown guidelines for .NET, C#, F#, VB.NET, TypeScript, JavaScript, React, Angular, Vue, Svelte, Next.js, Node.js, Docker, Git, REST APIs, GraphQL, SQL, PowerShell, Bash, and more. One always-on instruction (`copilot.instructions.md`) plus 59 toggleable instructions automatically attached to every Copilot Chat conversation when their technology is detected in the workspace.
- **12 MCP Tool Checks** across 4 server categories — C# coding style, naming conventions, async patterns, member ordering, nullable context, project structure, test style, NuGet hygiene (DotNet); commit format, commit content (Git); EditorConfig resolution (EditorConfig); TypeScript coding style (TypeScript).
- **EditorConfig-Driven Enforcement** — Checkers read `.editorconfig` properties and enforce whichever direction the project specifies rather than just skipping conflicting rules.
- **Workspace Detection** — Scans for project files, `package.json` dependencies, directory markers, and NuGet packages to set context keys that control which servers, tools, and instructions are active.
- **Auto Configuration** — One command scans the workspace and enables only the instructions and tools relevant to the detected technologies.
- **Status Bar** — A persistent indicator showing active instruction and tool counts (`$(book) X/59 $(tools) X/12`) with a quick-access menu for toggling instructions, toggling tools, or running auto-configure.
- **Toggle Menus** — Multi-select QuickPick menus for instructions and tools with category grouping, category-level toggling, and Select All / Clear All buttons.
- **Per-Instruction Disable** — Browse any instruction file in a virtual document, then use CodeLens actions to disable or re-enable individual rules without turning off the entire file. Disabled instructions are dimmed, tagged `[DISABLED]`, and excluded from Copilot's context.
- **Export** — Copy instruction files to `.github/instructions/` for team sharing. Exported instructions are automatically removed from the Toggle, Browse, and Export menus — they reappear if the exported file is deleted.
- **Multi-Window Safe** — Per-workspace staging directories with hash-based isolation and automatic cleanup of stale directories older than one hour.
- **Diagnostics** — Parses every instruction file on activation and logs warnings (missing IDs, duplicate IDs, malformed IDs) to the SharpPilot Output channel.

## Build Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 18+ — required to build the VS Code extension

## Repository Structure

```text
SharpPilot.slnx                        # Solution file
src/SharpPilot.WorkspaceServer/        # Workspace server (named-pipe, EditorConfig resolution)
src/SharpPilot.WorkspaceServer.Tests/  # xUnit tests for the workspace server
src/SharpPilot.Mcp.DotNet/            # MCP server (.NET + Git tools)
src/SharpPilot.Mcp.DotNet.Tests/      # xUnit tests for the .NET MCP server
src/SharpPilot.Mcp.Web/               # MCP server (TypeScript tools)
src/SharpPilot.VSCode/                # VS Code extension (instructions, tools, rule management)
```

## Architecture

See [docs/architecture.md](docs/architecture.md) for the full architecture guide — layer pipeline, activation and runtime flows, precedence rules, MCP servers, and tool reference.

## Manual Server Configuration

If you have the .NET 10 SDK installed and have cloned this repo, you can register the servers directly in `.vscode/mcp.json` without the VS Code extension. This is useful for development or for using the latest unreleased server code:

```jsonc
{
  "servers": {
    "dotnet-SharpPilot": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/SharpPilot.Mcp.DotNet/SharpPilot.Mcp.DotNet.csproj",
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
        "${workspaceFolder}/src/SharpPilot.Mcp.DotNet/SharpPilot.Mcp.DotNet.csproj",
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
        "${workspaceFolder}/src/SharpPilot.Mcp.DotNet/SharpPilot.Mcp.DotNet.csproj",
        "--",
        "--scope",
        "editorconfig"
      ]
    },
    "typescript-SharpPilot": {
      "type": "stdio",
      "command": "node",
      "args": [
        "${workspaceFolder}/src/SharpPilot.Mcp.Web/out/index.js",
        "--scope",
        "typescript"
      ]
    }
  }
}
```

> **Note:** The TypeScript server requires a prior build: `cd src/SharpPilot.Mcp.Web && npm install && npm run build`. When configured manually, EditorConfig properties are not resolved (the `--workspace-server` argument is omitted); checkers fall back to their built-in defaults.

## Testing

```powershell
./build.ps1                   # compile + test (all)
./build.ps1 Test TS           # TypeScript tests only
./build.ps1 Test DotNet       # .NET tests only
```

### VS Code Extension — Smoke Tests

Smoke tests launch a real VS Code instance, load the extension, and verify activation and command registration:

```sh
cd src/SharpPilot.VSCode
npm install
npm run test:smoke
```

A VS Code installation is downloaded automatically on the first run and cached
in `src/SharpPilot.VSCode/.vscode-test/`.

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

SharpPilot is licensed under the [AGPL-3.0](LICENSE). A separate [commercial license](COMMERCIAL.md) is available for organizations that want to use SharpPilot under terms different from the AGPL-3.0.

Use of the SharpPilot name and logo is subject to [TRADEMARKS.md](TRADEMARKS.md).

## Contributing

Contributions require acceptance of the [Contributor License Agreement](CLA.md). See [CONTRIBUTING.md](CONTRIBUTING.md) for how to get started.
