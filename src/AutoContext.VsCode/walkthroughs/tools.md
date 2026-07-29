## MCP Tools

AutoContext gives your AI coding agent a set of checks it can run on your code. In agent mode it calls them on the spot — typically before writing code, after editing, or while reviewing — and gets back a report it can act on.

The checks cover source files, project files, and commit messages, and can resolve the effective `.editorconfig` settings for any file.

### How the checks are grouped

| Group | Covers | Turned on when |
|----------|------------|----------------|
| **.NET** | C#, NuGet | A .NET project or solution is present |
| **Workspace** | Git, EditorConfig | Always available (Git checks appear once the folder is a Git repository) |
| **Web** | TypeScript | TypeScript files are present |

### First-time setup

The first time AutoContext offers its checks, VS Code asks you to confirm that you trust them. Accept the prompt so your agent can use them in agent mode.

### Turning checks on and off

Use the AutoContext sidebar to enable or disable individual checks. Checks are organised by group and category. Use the `…` menu on the panel header to show or hide items that don't apply to your project.

Your choices take effect immediately — nothing needs restarting, and your agent stops offering anything you switch off. They are saved in `.autocontext.json` in your workspace, so you can commit them to share with your team.

[Open Tools Panel](command:autocontext.mcp-tools-view.focus)
