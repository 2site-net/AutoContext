## Auto Configure

SharpPilot can scan your workspace and automatically enable the right instructions and tools for your project.

### What it detects

The workspace detector looks for files, project contents, and dependencies to build a context profile:

- **File existence** — Project files (`.csproj`, `.fsproj`, …), source files (`.ts`, `.razor`, …), configuration files (`Dockerfile`, `.git`), and more.
- **Project file contents** — NuGet packages, SDK references, and project properties (e.g., test frameworks, database drivers, ASP.NET Core, WPF, MAUI).
- **package.json dependencies** — Web frameworks, test runners, and libraries.

### How auto-configure works

For each instruction and tool, auto-configure checks whether any of its context keys match the workspace profile. Items with matching context are enabled; items without matching context are disabled. Items with no context keys (like general instructions) are always enabled.

### Status bar

The status bar shows the current counts at a glance. Click it to open a menu where you can toggle instructions, toggle tools, or run auto-configure.

### Run auto configure

Open a workspace with your project, then run:

[Auto Configure](command:sharppilot.autoConfigure)

You can always fine-tune the result afterwards using the toggle commands.
