## Auto Configure

SharpPilot can scan your workspace and automatically enable the right instructions and tools for your project.

### What it detects

The workspace detector looks for files, project contents, and dependencies to build a context profile:

- **File existence** — `.csproj`, `.fsproj`, `.vbproj`, `.razor`, `.html`, `.css`, `.js`, `.ts`, `Dockerfile`, `.git`, Unity project settings, and more.
- **Project file contents** — NuGet packages like `xunit`, `NUnit`, `MSTest`, `Entity Framework Core`, `Dapper`, `SignalR`, `gRPC`, `MediatR`, `Redis`, `HotChocolate`, and database drivers (SQL Server, PostgreSQL, MySQL, SQLite, Oracle, MongoDB). Also detects ASP.NET Core SDK, WPF, WinForms, and MAUI.
- **package.json dependencies** — Frameworks (`react`, `@angular/core`, `vue`, `svelte`, `next`), test runners (`vitest`, `jest`, `jasmine`, `mocha`, `@playwright/test`, `cypress`), and GraphQL libraries.

### How auto-configure works

For each instruction and tool, auto-configure checks whether any of its context keys match the workspace profile. Items with matching context are enabled; items without matching context are disabled. Items with no context keys (like general instructions) are always enabled.

### Status bar

The status bar shows the current counts at a glance: `$(book) X/59 $(tools) X/11`. Click it to open a menu where you can toggle instructions, toggle tools, or run auto-configure.

### Run auto configure

Open a workspace with your project, then run:

[Auto Configure](command:sharppilot.autoConfigure)

You can always fine-tune the result afterwards using the toggle commands.
