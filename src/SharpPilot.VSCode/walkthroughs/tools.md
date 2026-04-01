## MCP Tools

SharpPilot registers MCP (Model Context Protocol) servers that expose quality-assurance tools to Copilot. In agent mode, Copilot can call these tools to check your code on the spot.

### Server categories

Tools are organized into three server categories, each activated by workspace context:

| Category | Activates when | Built-in Checks |
|-------|---------------|-------|
| **DotNet** | `.csproj`, `.fsproj`, `.vbproj`, `.sln`, or `.slnx` detected | C# async patterns, coding style, member ordering, naming conventions, nullable context, project structure, test style, NuGet hygiene |
| **Git** | `.git` folder detected | Commit format, commit content |
| **EditorConfig** | Always active | EditorConfig check |

A server category is filtered out entirely if its workspace context is not present or all its tools are disabled. The EditorConfig category is an exception — it is always active regardless of workspace content.

### How it works

Enabled tool settings are written to a `.sharppilot.json` file that the .NET MCP server reads. Only enabled tools are exposed to Copilot, so disabling a tool removes it from agent mode entirely — with one exception: if the project's `.editorconfig` contains keys a checker consumes (e.g., `csharp_prefer_braces`), those EditorConfig-backed checks still run even when the tool is disabled.

### Toggle tools

Open the multi-select menu to enable or disable individual tools. Like instructions, tools are grouped by category with bulk select/deselect support.

[Toggle Tools](command:sharppilot.toggleTools)
