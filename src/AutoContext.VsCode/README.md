# AutoContext

AutoContext gives AI coding assistants the right context for your codebase. It provides built-in instructions, MCP tool checks, a dedicated tree view for managing them, and automatic context orchestration to deliver the right guidance and checks for the current task.

> **Work in Progress** — Instructions and tools are refined iteratively. Coverage, rules, and tool behavior will continue to evolve as we incorporate feedback and expand language and framework support.

## Features

- **Chat Instructions** — Curated Markdown guidelines covering C#, F#, VB.NET, TypeScript, JavaScript, Python, Java, Go, Rust, Ruby, Swift, Kotlin, Dart, C, C++, Scala, SQL, PowerShell, Bash, and more — plus .NET frameworks (ASP.NET Core, Blazor, EF Core, WPF, …), web frameworks (React, Angular, Vue, Svelte, Next.js, Node.js, …), and tools (Git, Docker). Instructions are workspace-aware — only the ones relevant to your project are injected into Copilot's context.
- **MCP Tool Checks** — Quality checks that Copilot can invoke in Agent mode. Categories include .NET (C# style, naming, async patterns, NuGet hygiene, …), Workspace (Git commit format and content, EditorConfig property resolution), and Web (TypeScript coding style). Each feature can be toggled individually.
- **EditorConfig-Driven Enforcement** — Checkers read `.editorconfig` properties and enforce whichever direction the project specifies rather than just skipping conflicting rules.
- **Workspace Detection** — Scans for project files, dependencies, and directory markers to automatically determine which servers, tools, and instructions are relevant.
- **Auto Configuration** — One command scans the workspace and enables only the instructions and tools that match the detected technologies.
- **Sidebar Panels** — A dedicated AutoContext activity bar with two tree views: **Instructions** (grouped by category) and **MCP Tools** (grouped by group and category). Each panel header shows the enabled/total count, and the `…` menu includes a filter to show or hide items not detected in the workspace.
- **Per-Instruction Disable** — Click any instruction in the sidebar to open it in a virtual document, then use CodeLens actions to disable or re-enable individual rules without turning off the entire file.
- **Export** — Enter export mode from the Instructions panel header, check the instructions you want to export, and confirm. Files are copied to `.github/instructions/` for team sharing. Exported instructions appear as **overridden** in the panel — the workspace-level file takes precedence. Delete the exported file to revert to the built-in version.
- **Override Staleness Detection** — When a local override in `.github/instructions/` is older than the bundled version, it is flagged as `"overridden (outdated)"` in the tree view. Deleting an outdated override shows a version-comparison dialog and restores the latest built-in version. Use **Show Original** to compare or **Show Changelog** to review what changed.
- **Upgrade Awareness** — A badge appears on the Instructions panel when the extension updates. Disabled instruction IDs are automatically cleared when an instruction's rule set changes (major or minor version bump), with a notification explaining which files were affected.

## MCP Tools

Once installed, the following MCP tools are available to GitHub Copilot in Agent mode. Ask Copilot to check your code or commits and it will invoke the relevant tool.

| Category | Tool | Purpose |
|----------|------|---------|
| .NET | `check_csharp_all` | Composite C# quality check (style, naming, async, structure, …) |
| .NET | `check_nuget_hygiene` | Package version and hygiene check |
| Workspace | `check_git_all` | Conventional Commits format and content check |
| Workspace | `get_editorconfig` | Resolve effective `.editorconfig` properties for a file |
| Web | `check_typescript_all` | Composite TypeScript quality check |

Each category maps to a dedicated MCP server. Tools within a category are further organized by sub-category (e.g., C#, NuGet under .NET) and can be toggled individually under **Settings → AutoContext** (search `autocontext.mcpTools`), or from the **MCP Tools** panel in the AutoContext sidebar. If all tools for a category are disabled, that server is not registered at all.

## Sidebar Panels

AutoContext adds a dedicated activity bar icon with two tree views:

- **Instructions** — Grouped by category (General, Languages, .NET, Web, Tools). Click an instruction to open it in a virtual document with per-rule CodeLens. Enable or disable instructions from the inline actions. Enter export mode from the panel header to batch-export checked instructions to `.github/instructions/`.
- **MCP Tools** — Grouped by platform (.NET, Web, Workspace), category (C#, NuGet, TypeScript, Git, EditorConfig), and tool. Check or uncheck an MCP tool to toggle all its features at once. Individual features can also be toggled.

Both panels show an **enabled / total** count in the header and offer a **Show Not Detected** / **Hide Not Detected** filter in the `…` overflow menu.

## Per-Instruction Disable

Individual rules within any instruction file can be disabled without turning off the entire instruction:

1. Click an instruction in the **Instructions** sidebar panel.
2. The file opens in a virtual document with a **Disable Instruction** / **Enable Instruction** CodeLens above each rule.
3. Click a CodeLens to toggle. Disabled rules are dimmed, tagged `[DISABLED]`, and excluded from Copilot's context.
4. A **Reset All Instructions** CodeLens appears at the top to re-enable everything at once.

The disable state is stored in `.autocontext.json` in your workspace root — commit it for team-wide settings or add it to `.gitignore` for personal preferences.

## Commands

| Command | Description |
|---------|-------------|
| **AutoContext: Auto Configure** | Scan the workspace and enable relevant items. |
| **AutoContext: Toggle Instruction** | Disable or re-enable a single instruction (invoked via CodeLens). |
| **AutoContext: Reset Instructions** | Re-enable all disabled instructions for the current file (invoked via CodeLens). |
| **AutoContext: Enable Instruction** | Enable an instruction from the sidebar panel. |
| **AutoContext: Disable Instruction** | Disable an instruction from the sidebar panel. |
| **AutoContext: Export Instructions** | Enter export mode — check instructions to export to `.github/instructions/`. |
| **AutoContext: Delete Override** | Remove an exported instruction file from the workspace. |
| **AutoContext: Show Original** | View the built-in version of an overridden instruction. |
| **AutoContext: Show Changelog** | Open the version history for an instruction (when available). |
| **AutoContext: Show What's New** | Open the extension release notes. |
| **AutoContext: Show Not Detected** | Show items not detected in the workspace in the sidebar panels. |
| **AutoContext: Hide Not Detected** | Hide items not detected in the workspace from the sidebar panels. |

## Prerequisites

- VS Code 1.100 or later with [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot).

No .NET runtime is required — the extension ships self-contained executables.

## Installation

Install the platform-specific `.vsix` for your OS from the Extensions view (**Install from VSIX…**) or from the command line:

```sh
code --install-extension AutoContext-win32-x64-0.5.0.vsix
```

Once installed, open Agent mode in Copilot Chat and the AutoContext tools will appear in the tools picker. Ask Copilot things like:

- *"Check this file for code style issues."*
- *"Validate my commit message against Conventional Commits."*
- *"Check for async pattern violations in the current file."*

You can verify the servers are running via the Command Palette → **MCP: List Servers**.

## License

AutoContext is licensed under the [AGPL-3.0](LICENSE). A separate [commercial license](COMMERCIAL.md) is available for organizations that want to use AutoContext under terms different from the AGPL-3.0.

Use of the AutoContext name and logo is subject to [TRADEMARKS.md](TRADEMARKS.md).

## Source

[github.com/2site-net/AutoContext](https://github.com/2site-net/AutoContext) — see [CONTRIBUTING.md](https://github.com/2site-net/AutoContext/blob/main/CONTRIBUTING.md) for contribution guidelines.
