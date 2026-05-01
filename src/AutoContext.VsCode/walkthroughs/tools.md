## MCP Tools

AutoContext registers a single MCP (Model Context Protocol) server (`AutoContext.Mcp.Server`) that lets Copilot analyze source files, project manifests, and commit messages against the conventions the chat instructions describe, and resolve the effective `.editorconfig` properties for any path. In agent mode, Copilot calls these tools on the spot — typically before generating, after editing, or while reviewing — and gets back a structured report it can act on. Each call is dispatched to the worker process that owns the tool: .NET, Workspace, or Web.

### Tool organization

Tools are grouped by **platform**, **category**, and **tool**, and each platform is activated by workspace context:

| Platform | Categories | Activates when |
|----------|------------|----------------|
| **.NET** | C#, NuGet | `.csproj`, `.fsproj`, `.vbproj`, `.sln`, or `.slnx` detected |
| **Workspace** | Git, EditorConfig | Always active (Git tools surface only when a `.git` folder is present) |
| **Web** | TypeScript | `.ts` files detected |

If every tool owned by a worker is disabled, that worker is not spawned at all.

### Enable the MCP server

When AutoContext detects your workspace, it registers its MCP server with VS Code. The first time the server is discovered, VS Code prompts you to confirm that you trust and want to start it. Accept the prompt so Copilot can invoke the server tools in agent mode.

### Server health monitoring

The MCP server and each worker report their liveness via a health monitoring pipe. Server nodes in the MCP Tools panel show a live **running** or **stopped** status. Workers spawn lazily: a worker process is only started the first time a tool it owns is invoked (the workspace worker also starts during extension activation, since EditorConfig resolution depends on it). When every tool a worker owns is disabled, that worker process is never started and its server node stays **stopped**. Use the inline **Start** and **Show Output** actions on each server node to bring the MCP server up or open its log.

### How it works

When you disable a feature, it is recorded in `.autocontext.json` at your workspace root. The workspace worker reads this file and decides how each feature runs. Disabled features are skipped when Copilot invokes the tool — with one exception: if the project's `.editorconfig` contains keys a checker consumes (e.g., `csharp_prefer_braces`), those EditorConfig-backed checks still apply even when the feature is disabled.

### Toggle tools

Use the AutoContext sidebar to enable or disable individual tools. Tools are organized under platform, category, and tool headers — checking an MCP tool toggles all its features at once. Use the `…` menu on the panel header to show or hide items that are not detected in your workspace.

[Open Tools Panel](command:autocontext.mcp-tools-view.focus)
