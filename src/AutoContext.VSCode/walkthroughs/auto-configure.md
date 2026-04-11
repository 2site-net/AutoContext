## Auto Configure

AutoContext can scan your workspace and automatically enable the right instructions and tools for your project.

### What it detects

The workspace detector looks for files, project contents, and dependencies to build a context profile:

- **File existence** — Project files (`.csproj`, `.fsproj`, …), source files (`.ts`, `.razor`, …), configuration files (`Dockerfile`, `.git`), and more.
- **Project file contents** — NuGet packages, SDK references, and project properties (e.g., test frameworks, database drivers, ASP.NET Core, WPF, MAUI).
- **package.json dependencies** — Web frameworks, test runners, and libraries.

### How auto-configure works

For each instruction and tool, auto-configure checks whether any of its context keys match the workspace profile. Items with matching context are enabled; items without matching context are disabled. Items with no context keys (like general instructions) are always enabled.

### Sidebar panels

The Instructions and MCP Tools panels in the AutoContext activity bar show the enabled/total count in each panel header, so you can see the result at a glance.

### Run auto configure

Open a workspace with your project, then run:

[Auto Configure](command:autocontext.auto-configure)

You can always fine-tune the result afterwards from the sidebar panels.
