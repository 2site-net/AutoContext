![SharpPilot](small-logo.png)

# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

Distributed as a VS Code extension — see [src/SharpPilot.VSCode/README.md](src/SharpPilot.VSCode/README.md) for installation and usage.

## Features

- **60 Chat Instructions** — Curated Markdown guidelines for .NET, C#, F#, VB.NET, TypeScript, JavaScript, React, Angular, Vue, Svelte, Next.js, Node.js, Docker, Git, REST APIs, GraphQL, SQL, PowerShell, Bash, and more. One always-on instruction (`copilot.instructions.md`) plus 59 toggleable instructions automatically attached to every Copilot Chat conversation when their technology is detected in the workspace.
- **11 MCP Tool Checks** across 3 server categories — C# coding style, naming conventions, async patterns, member ordering, nullable context, project structure, test style, NuGet hygiene (DotNet); commit format, commit content (Git); EditorConfig resolution (EditorConfig).
- **EditorConfig-Driven Enforcement** — Checkers read `.editorconfig` properties and enforce whichever direction the project specifies rather than just skipping conflicting rules.
- **Workspace Detection** — Scans for project files, `package.json` dependencies, directory markers, and NuGet packages to set context keys that control which servers, tools, and instructions are active.
- **Auto Configuration** — One command scans the workspace and enables only the instructions and tools relevant to the detected technologies.
- **Status Bar** — A persistent indicator showing active instruction and tool counts (`$(book) X/59 $(tools) X/11`) with a quick-access menu for toggling instructions, toggling tools, or running auto-configure.
- **Toggle Menus** — Multi-select QuickPick menus for instructions and tools with category grouping, category-level toggling, and Select All / Clear All buttons.
- **Per-Instruction Disable** — Browse any instruction file in a virtual document, then use CodeLens actions to disable or re-enable individual rules without turning off the entire file. Disabled instructions are dimmed, tagged `[DISABLED]`, and excluded from Copilot's context.
- **Export** — Copy instruction files to `.github/instructions/` for team sharing. Exported instructions are automatically removed from the Toggle, Browse, and Export menus — they reappear if the exported file is deleted.
- **Multi-Window Safe** — Per-workspace staging directories with hash-based isolation and automatic cleanup of stale directories older than one hour.
- **Diagnostics** — Parses every instruction file on activation and logs warnings (missing IDs, duplicate IDs, malformed IDs) to the SharpPilot Output channel.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 18+ — required to build the VS Code extension

## Repository Structure

```text
SharpPilot.slnx                  # Solution file
src/SharpPilot/                  # MCP server (.NET + Git tools)
tests/SharpPilot.Tests/          # xUnit tests
src/SharpPilot.VSCode/            # VS Code extension (instructions, tools, rule management)
```

## Architecture

SharpPilot operates as a multi-layer pipeline where each layer feeds
deterministic, workspace-specific context into the next. The layers run in
order and the output of each becomes the input of the one below it.

### Layer overview

```text
┌─────────────────────────────────────────────────────────────────────┐
│  1. Workspace Detection                                             │
│     WorkspaceContextDetector scans the workspace and sets boolean   │
│     context keys (hasDotnet, hasGit, hasTypeScript, …).             │
├─────────────────────────────────────────────────────────────────────┤
│  2. Server Registration                                             │
│     The extension registers one MCP server per category (dotnet,    │
│     git, editorconfig). A server is only registered when its        │
│     context key is true AND at least one of its tools is enabled    │
│     in settings.                                                    │
├─────────────────────────────────────────────────────────────────────┤
│  3. Instruction Injection                                           │
│     60 instruction files are conditionally injected into Copilot's  │
│     context based on the same context keys. Per-instruction disable │
│     (via .sharppilot.json) removes individual rules without         │
│     turning off the entire file.                                    │
├─────────────────────────────────────────────────────────────────────┤
│  4. Tool Configuration                                              │
│     ToolsStatusWriter reads VS Code settings and writes disabled    │
│     tool names to .sharppilot.json. The MCP server reads this file  │
│     at runtime to skip disabled sub-checks.                         │
├─────────────────────────────────────────────────────────────────────┤
│  5. Runtime (Copilot invokes tools)                                 │
│     Copilot calls check_dotnet / check_nuget_hygiene /              │
│     check_git_commit / get_editorconfig. The server resolves        │
│     .editorconfig properties and uses them to drive checker         │
│     behavior — e.g., enforcement direction for brace style and      │
│     namespace style.                                                │
└─────────────────────────────────────────────────────────────────────┘
```

### Activation flow

When the extension activates, the following steps execute synchronously:

1. **`ToolsStatusWriter.write()`** — reads VS Code settings, writes disabled
   tool names to `.sharppilot.json` in the workspace root.
2. **`WorkspaceContextDetector.detect()`** — scans the workspace for project
   files, `package.json` dependencies, and directory markers. Sets VS Code
   context keys that control both server registration and instruction injection.
3. **`ConfigManager.removeOrphanedIds()`** — cleans disabled-instruction IDs
   from `.sharppilot.json` that no longer match any instruction in the current
   extension version.
4. **`InstructionsWriter.removeOrphanedStagingDirs()`** — deletes per-workspace
   staging directories older than one hour that belong to other VS Code windows.
5. **`InstructionsWriter.write()`** — normalizes all instruction files into
   `instructions/.generated/`, stripping `[INSTxxxx]` tag identifiers and
   removing any individually disabled instruction bullets. Copilot always
   reads from the normalized output, so neither tags nor disabled content
   are visible to the model.
6. **`logDiagnostics()`** — parses every instruction file and logs warnings
   (e.g., missing `[INSTxxxx]` IDs) to the **SharpPilot** Output channel.

### Runtime flow

When Copilot invokes an MCP tool (e.g., `check_dotnet`):

1. The MCP server reads `.sharppilot.json` → skips any disabled sub-checks.
2. If `editorConfigFilePath` is provided, the server resolves `.editorconfig`
   properties and merges them into the checker data.
3. Each checker reads the merged EditorConfig values and uses them to **drive**
   its enforcement direction — not just to skip conflicting checks.
4. The checker returns a report (✅ pass or ❌ violations found).

### Precedence

When multiple sources disagree, the following precedence applies:

| Priority | Source | Role |
|----------|--------|------|
| 1 | `.editorconfig` | Drives enforcement direction — checkers enforce whatever EditorConfig says. Instruction defaults yield to EditorConfig values. |
| 2 | Instruction files | Provide default coding guidance. Style rules in instructions are fallback defaults, not absolutes. |
| 3 | VS Code settings / `.sharppilot.json` | Control which tools and instructions are active. |
| 4 | Workspace context | Determines which servers and instructions are registered at all. |

See the "EditorConfig wins" rule in `copilot.instructions.md` for the
user-facing statement of this precedence.

## Servers and Tools

The MCP server exposes three tool categories. The VS Code extension registers
the server three times — `--scope dotnet`, `--scope git`, and
`--scope editorconfig` — so that .NET, Git, and EditorConfig tools appear in
separate sections in the tools UI.

Servers are workspace-aware: the extension only registers a server when the
workspace contains matching content (e.g., `.csproj` files for .NET, `.git`
directory for Git). The EditorConfig server is always available regardless of
workspace content. If all sub-checks for a category are disabled in settings,
that server is not registered at all.

### SharpPilot: DotNet

Two tools that analyse C# source and project files for common quality issues.

| Tool | Purpose |
|------|---------|
| `check_dotnet` | Runs all enabled .NET code quality checks on C# source and returns a combined report. Covers coding style, member ordering, naming conventions, async patterns, nullable context, project structure, and test style. When an `.editorconfig` path is provided, resolves its properties and uses them to drive checker behavior (e.g., brace and namespace style enforcement direction). |
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

### Viewing Tool Invocation Logs

Each server logs tool invocations (tool name, content length, data keys) to
stderr, which VS Code surfaces in the **Output** panel. To view the logs:

1. Open the **Output** panel (`Ctrl+Shift+U`).
2. Select the server from the dropdown (e.g., *SharpPilot: DotNet*).

Only SharpPilot log messages are emitted — host and framework noise is filtered
out.

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

## Testing

### MCP Server (xUnit)

```sh
dotnet test tests/SharpPilot.Tests/SharpPilot.Tests.csproj
```

### VS Code Extension — Unit Tests (Vitest)

```sh
cd src/SharpPilot.VSCode
npm install
npm test
```

### VS Code Extension — Smoke Tests

Smoke tests launch a real VS Code instance, load the extension, and verify
activation and command registration:

```sh
cd src/SharpPilot.VSCode
npm install
npm run test:smoke
```

A VS Code installation is downloaded automatically on the first run and cached
in `src/SharpPilot.VSCode/.vscode-test/`.

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

This project uses a dual-license model:

- **MCP Server** (`src/SharpPilot/`) — [AGPL-3.0](LICENSE)
- **VS Code Extension** (`src/SharpPilot.VSCode/`) — [GPL-3.0](src/SharpPilot.VSCode/LICENSE)

See each LICENSE file for full terms. By contributing, you agree to the [Contributor License Agreement](CLA.md).

