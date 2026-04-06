## MCP Tools

SharpPilot registers MCP (Model Context Protocol) servers that expose quality-assurance tools to Copilot. In agent mode, Copilot can call these tools to check your code on the spot.

### Server categories

Tools are organized into server categories, each activated by workspace context:

| Category | Activates when |
|----------|----------------|
| **DotNet** | `.csproj`, `.fsproj`, `.vbproj`, `.sln`, or `.slnx` detected |
| **Git** | `.git` folder detected |
| **EditorConfig** | Always active |
| **TypeScript** | `.ts` files detected |

A server category is filtered out entirely if its workspace context is not present or all its tools are disabled. The EditorConfig category is an exception — it is always active regardless of workspace content. Each category exposes one or more tools containing individually toggleable sub-checks.

### How it works

Enabled tool settings are written to a `.sharppilot.json` file that the .NET MCP server reads. Only enabled tools are exposed to Copilot, so disabling a tool removes it from agent mode entirely — with one exception: if the project's `.editorconfig` contains keys a checker consumes (e.g., `csharp_prefer_braces`), those EditorConfig-backed checks still run even when the tool is disabled.

### Toggle tools

Use the SharpPilot sidebar to enable or disable individual tools. Tools are organized under group and category headers — checking a category toggles all tools within it. Use the `…` menu on the panel header to show or hide items that are not detected in your workspace.

[Open Tools Panel](command:sharppilot.toolsView.focus)
