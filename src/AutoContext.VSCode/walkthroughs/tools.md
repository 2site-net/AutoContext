## MCP Tools

AutoContext registers MCP (Model Context Protocol) servers that expose quality-assurance tools to Copilot. In agent mode, Copilot can call these tools to check your code on the spot.

### Server categories

Tools are organized into server categories, each activated by workspace context:

| Category | Activates when |
|----------|----------------|
| **DotNet** | `.csproj`, `.fsproj`, `.vbproj`, `.sln`, or `.slnx` detected |
| **Git** | `.git` folder detected |
| **EditorConfig** | Always active |
| **TypeScript** | `.ts` files detected |

A server category is filtered out entirely if its workspace context is not present or all its tools are disabled. The EditorConfig category is an exception — it is always active regardless of workspace content. Each category exposes one or more MCP tools containing individually toggleable features.

### How it works

When you disable a feature, it is recorded in `.autocontext.json` at your workspace root. The workspace server reads this file and decides how each feature runs. Disabled features are skipped when Copilot invokes the tool — with one exception: if the project's `.editorconfig` contains keys a checker consumes (e.g., `csharp_prefer_braces`), those EditorConfig-backed checks still apply even when the feature is disabled.

### Toggle tools

Use the AutoContext sidebar to enable or disable individual tools. Tools are organized under platform, category, and tool headers — checking an MCP tool toggles all its features at once. Use the `…` menu on the panel header to show or hide items that are not detected in your workspace.

[Open Tools Panel](command:autocontext.mcp-tools-view.focus)
