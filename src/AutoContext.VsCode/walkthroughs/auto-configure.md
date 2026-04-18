## Auto Configure

AutoContext scans your workspace on activation and maintains detection state incrementally via file-system watchers — changes to source files or project manifests trigger targeted re-scans. The right instructions and tools are enabled automatically — no manual step is required. You can also trigger a full scan manually with the command below.

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

Auto-configuration runs automatically, but you can also trigger it manually:

[Auto Configure](command:autocontext.auto-configure)

You can always fine-tune the result afterwards from the sidebar panels.
