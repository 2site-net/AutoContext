![AutoContext](logo.png)

# AutoContext

AutoContext gives AI coding assistants the right context for your codebase. It provides built-in instructions, MCP tool checks, a dedicated tree view for managing them, and automatic context orchestration to deliver the right guidance and checks for the current task. Today AutoContext is integrated with GitHub Copilot, but its architecture is designed to support additional coding assistants and external context sources over time.

> **Work in Progress** — Instructions and tools are refined iteratively. Coverage, rules, and tool behavior will continue to evolve as we incorporate feedback and expand language and framework support.

Distributed as a VS Code extension — see [src/AutoContext.VsCode/README.md](src/AutoContext.VsCode/README.md) for installation and usage.

## Features

AutoContext provides two complementary capabilities:

- **Chat Instructions** — Curated Markdown guidelines covering .NET, TypeScript, Web frameworks, Git, scripting, and more. Instructions are workspace-aware — only the ones relevant to your project are injected into Copilot's context. Individual rules within any instruction file can be disabled without turning off the entire file.
- **MCP Tool Checks** — Quality checks that Copilot can invoke in Agent mode to validate code style, naming conventions, async patterns, NuGet hygiene, commit messages, and more. Checkers read `.editorconfig` properties and enforce whichever direction the project specifies.

Tools and instructions are grouped into categories and managed from dedicated sidebar panels — individually toggled, auto-configured based on workspace detection, or exported to `.github/instructions/` for team sharing. Exported instructions are tracked for staleness and flagged when newer built-in versions are available. MCP servers are health-monitored via a named-pipe protocol, with live status indicators and inline Start / Show Output controls in the sidebar; workers spawn lazily on first use.

## Build Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 18+ — required to build the VS Code extension

## Repository Structure

```text
AutoContext.slnx                          # Solution file
servers.json                              # Server manifest (id + name + runtime kind for the orchestrator and each worker)
version.json                              # Canonical version (single source of truth)
versionize.ps1                            # Version stamping tool (Sync, Export, SyncAndExport)
src/AutoContext.Mcp.Abstractions/         # IMcpTask contract shared between the orchestrator and every worker
src/AutoContext.Framework/                # Hosting, structured logging, and worker-side framework primitives
src/AutoContext.Worker.Shared/            # Shared hosting helpers used by the .NET workers
src/AutoContext.Mcp.Server/               # Single MCP/stdio orchestrator. Loads the embedded mcp-workers-registry.json,
                                          # exposes every declared tool to the MCP client, and dispatches each call to the
                                          # owning AutoContext.Worker.* over a named pipe.
  Registry/                               #   Registry loader, schema validator, and McpWorkersCatalog
  Workers/                                #   WorkerClient + Protocol/Transport (named-pipe RPC to the workers)
  Tools/                                  #   McpSdkAdapter + invocation/results pipeline exposed to MCP clients
  EditorConfig/                           #   In-process EditorConfig resolution shared by tools that need it
  mcp-workers-registry.json               #   Embedded registry: every tool, its parameters, and its owning worker
src/AutoContext.Worker.DotNet/            # .NET worker. Hosts C# and NuGet analyzers (Tasks/CSharp, Tasks/NuGet).
src/AutoContext.Worker.Workspace/         # .NET worker. Hosts Git and EditorConfig tasks.
src/AutoContext.Worker.Web/               # Node.js / TypeScript worker. Hosts the TypeScript analyzer.
src/AutoContext.VsCode/                   # VS Code extension. Spawns Mcp.Server + every worker, runs the LogServer
                                          # and HealthMonitorServer pipes, and ships the instructions, sidebar panels,
                                          # workspace detection, and per-instruction configuration UI.
src/tests/                                # All test projects (Framework, Worker.Shared, Worker.Workspace,
                                          # Worker.DotNet, Mcp.Server, plus the Worker.Testing helper library)
```

## Architecture

See [docs/architecture.md](docs/architecture.md) for the full architecture guide — layer pipeline, activation and runtime flows, precedence rules, MCP servers, and tool reference.

## Running Outside VS Code

The canonical way to use AutoContext is through the VS Code extension, which
spawns `AutoContext.Mcp.Server` (the single MCP/stdio orchestrator) along with
every `AutoContext.Worker.*` sidecar and wires them together over named pipes.

If you need to use AutoContext from another MCP client, register only the
orchestrator — it exposes every tool declared in the embedded
`mcp-workers-registry.json` and dispatches each call to the owning worker:

```jsonc
{
  "servers": {
    "AutoContext": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/AutoContext.Mcp.Server/AutoContext.Mcp.Server.csproj"
      ]
    }
  }
}
```

> **Note:** The orchestrator expects every worker named in
> `mcp-workers-registry.json` to already be listening on its named pipe before
> a tool call is dispatched. The VS Code extension manages this
> spawn/lifecycle for you (`worker-manager.ts`); standalone setups must
> launch each `AutoContext.Worker.*` separately and keep them alive. The
> Node.js worker (`AutoContext.Worker.Web`) requires a prior build:
> `cd src/AutoContext.Worker.Web && npm install && npm run build`.

## Testing

```powershell
./build.ps1                   # compile + test (all)
./build.ps1 Test TS           # TypeScript tests only
./build.ps1 Test DotNet       # .NET tests only
```

### VS Code Extension — Smoke Tests

Smoke tests launch a real VS Code instance, load the extension, and verify activation and command registration:

```powershell
./build.ps1 Test -Smoke
```

A VS Code installation is downloaded automatically on the first run and cached
in `src/AutoContext.VsCode/.vscode-test/`.

## Building and Publishing the Extension

```powershell
./build.ps1 Package                          # current platform
./build.ps1 Package All                      # all 6 platforms
./build.ps1 Package -RuntimeIdentifier win-x64
```

Available targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

Publish to the VS Code Marketplace and [Open VSX](https://open-vsx.org/) registry:

```powershell
./build.ps1 Publish           # current platform
./build.ps1 Publish All       # all 6 platforms
```

Publishing requires two Personal Access Tokens set as environment variables:

| Variable   | Source | Scope |
|------------|--------|-------|
| `VSCE_PAT` | [Azure DevOps](https://dev.azure.com/_usersSettings/tokens) | **Marketplace → Manage** |
| `OVSX_PAT` | [Open VSX](https://open-vsx.org/) → Access Tokens | publish access |

## Releasing

The `Tag` action bumps all version numbers, compiles, tests, commits, and creates an annotated git tag in one step:

```powershell
./build.ps1 Tag 0.6.0           # bump to 0.6.0, compile, test, commit, tag
./build.ps1 Tag 0.6.0 -WhatIf   # dry-run — shows what would happen without changing anything
```

The action:

1. Validates the version string (semver: `X.Y.Z` or `X.Y.Z-prerelease`).
2. Rejects versions lower than the current version in `version.json`.
3. Requires a clean working tree (no uncommitted changes).
4. Compiles and tests the entire solution — the tag is only created if everything passes.
5. Updates `version.json`, then runs `versionize.ps1 Sync` to stamp all `package.json`, `package-lock.json`, and `.csproj` files, and `Export` to generate `version.ts` constants for Node.js servers. Commits the result.
6. Creates an annotated tag (`Release X.Y.Z`).

If the requested version already matches the current version (e.g. after a failed push), the bump and commit are skipped and only the tag is created.

After the script completes, push the tag to trigger CI:

```powershell
git push origin main --follow-tags
```

## License

AutoContext is licensed under the [AGPL-3.0](LICENSE). A separate [commercial license](COMMERCIAL.md) is available for organizations that want to use AutoContext under terms different from the AGPL-3.0.

Use of the AutoContext name and logo is subject to [TRADEMARKS.md](TRADEMARKS.md).

## Contributing

Contributions require acceptance of the [Contributor License Agreement](CLA.md). See [CONTRIBUTING.md](CONTRIBUTING.md) for how to get started.
