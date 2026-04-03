![SharpPilot](small-logo.png)

# SharpPilot

SharpPilot is a quality assurance extension for Visual Studio Code that leverages an MCP server to enable model-invokable tools and curated, configurable instructions—elevating code quality, workflows, and overall developer productivity with Copilot.

## Features

- **Chat Instructions** — Curated Markdown guidelines covering .NET, C#, F#, VB.NET, TypeScript, JavaScript, React, Angular, Vue, Svelte, Next.js, Node.js, Docker, Git, REST APIs, GraphQL, SQL, PowerShell, Bash, and more. Instructions are workspace-aware — only the ones relevant to your project are injected into Copilot's context.
- **MCP Tool Checks** — Quality checks that Copilot can invoke in Agent mode. Categories include DotNet (C# style, naming, async patterns, NuGet hygiene, …), Git (commit format and content), EditorConfig (property resolution), and TypeScript (coding style). Each sub-check can be toggled individually.
- **EditorConfig-Driven Enforcement** — Checkers read `.editorconfig` properties and enforce whichever direction the project specifies rather than just skipping conflicting rules.
- **Workspace Detection** — Scans for project files, dependencies, and directory markers to automatically determine which servers, tools, and instructions are relevant.
- **Auto Configuration** — One command scans the workspace and enables only the instructions and tools that match the detected technologies.
- **Toggle Menus** — Multi-select QuickPick menus for instructions and tools with category grouping, category-level toggling, and Select All / Clear All buttons.
- **Per-Instruction Disable** — Browse any instruction file in a virtual document, then use CodeLens actions to disable or re-enable individual rules without turning off the entire file.
- **Export** — Copy instruction files to `.github/instructions/` for team sharing. Exported instructions are automatically hidden from the extension menus — delete the exported file to bring them back.
- **Status Bar** — A persistent indicator showing active instruction and tool counts with a quick-access menu for toggling and auto-configuration.

## MCP Tools

Once installed, the following aggregation tools are available to GitHub Copilot in Agent mode. Ask Copilot to check your code or commits and it will invoke the relevant tool.

| Category | Tool | Purpose |
|----------|------|---------|
| DotNet | `check_csharp_all` | Composite C# quality check (style, naming, async, structure, …) |
| DotNet | `check_nuget_hygiene` | Package version and hygiene check |
| Git | `check_git_all` | Conventional Commits format and content check |
| EditorConfig | `get_editorconfig` | Resolve effective `.editorconfig` properties for a file |
| TypeScript | `check_typescript_all` | Composite TypeScript quality check |

Each aggregation tool bundles multiple sub-checks that can be toggled individually under **Settings → SharpPilot → Tools**, or via **SharpPilot: Toggle Tools** in the Command Palette. If all sub-checks for a category are disabled, that server is not registered at all.

## Per-Instruction Disable

Individual rules within any instruction file can be disabled without turning off the entire instruction:

1. Run **SharpPilot: Browse Instructions** and select an instruction.
2. The file opens in a virtual document with a **Disable Instruction** / **Enable Instruction** CodeLens above each rule.
3. Click a CodeLens to toggle. Disabled rules are dimmed, tagged `[DISABLED]`, and excluded from Copilot's context.
4. A **Reset All Instructions** CodeLens appears at the top to re-enable everything at once.

The disable state is stored in `.sharppilot.json` in your workspace root — commit it for team-wide settings or add it to `.gitignore` for personal preferences.

## Commands

| Command | Description |
|---------|-------------|
| **SharpPilot: Toggle Instructions** | Enable or disable coding instructions. |
| **SharpPilot: Toggle Tools** | Enable or disable individual tool checks. |
| **SharpPilot: Auto Configure** | Scan the workspace and enable relevant items. |
| **SharpPilot: Export Instructions** | Export instruction files to `.github/instructions/`. |
| **SharpPilot: Browse Instructions** | Preview an instruction file with per-instruction disable/enable CodeLens. |
| **SharpPilot: Toggle Instruction** | Disable or re-enable a single instruction (invoked via CodeLens). |
| **SharpPilot: Reset Instructions** | Re-enable all disabled instructions for the current file (invoked via CodeLens). |

## Prerequisites

- VS Code 1.100 or later with [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot).

No .NET runtime is required — the extension ships self-contained executables.

## Installation

Install the platform-specific `.vsix` for your OS from the Extensions view (**Install from VSIX…**) or from the command line:

```sh
code --install-extension SharpPilot-win32-x64-0.5.0.vsix
```

Once installed, open Agent mode in Copilot Chat and the SharpPilot tools will appear in the tools picker. Ask Copilot things like:

- *"Check this file for code style issues."*
- *"Validate my commit message against Conventional Commits."*
- *"Check for async pattern violations in the current file."*

You can verify the servers are running via the Command Palette → **MCP: List Servers**.

## License

SharpPilot is licensed under the [AGPL-3.0](LICENSE). A separate [commercial license](COMMERCIAL.md) is available for organizations that want to use SharpPilot under terms different from the AGPL-3.0.

Use of the SharpPilot name and logo is subject to [TRADEMARKS.md](TRADEMARKS.md).

## Source

[github.com/2site-net/SharpPilot](https://github.com/2site-net/SharpPilot) — see [CONTRIBUTING.md](https://github.com/2site-net/SharpPilot/blob/main/CONTRIBUTING.md) for contribution guidelines.
