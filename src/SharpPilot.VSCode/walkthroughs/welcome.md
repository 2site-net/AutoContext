## Welcome to SharpPilot

SharpPilot is a quality-assurance companion for GitHub Copilot. It ships curated **chat instructions** that guide Copilot's responses and registers **MCP servers** whose tools let Copilot check your code against best practices — all configurable from a single status-bar entry point.

### What you get

- **60 Chat Instructions** — Curated Markdown guidelines for .NET, C#, F#, VB.NET, TypeScript, JavaScript, React, Angular, Vue, Svelte, Next.js, Node.js, Docker, Git, REST APIs, GraphQL, SQL, PowerShell, Bash, and more. One always-on instruction (`copilot.instructions.md`) plus 59 toggleable instructions automatically attached to every Copilot Chat conversation when their technology is detected in the workspace.
- **11 MCP Tool Checks** across 3 server scopes — C# coding style, naming conventions, async patterns, member ordering, nullable context, project structure, test style, NuGet hygiene (DotNet); commit format, commit content (Git); EditorConfig resolution (EditorConfig).
- **EditorConfig-Driven Enforcement** — Checkers read `.editorconfig` properties and enforce whichever direction the project specifies rather than just skipping conflicting rules.
- **Workspace Detection** — Scans for project files, `package.json` dependencies, directory markers, and NuGet packages to set context keys that control which servers, tools, and instructions are active.
- **Auto Configuration** — One command scans the workspace and enables only the instructions and tools relevant to the detected technologies.
- **Status Bar** — A persistent indicator showing active instruction and tool counts (`$(book) X/59 $(tools) X/11`) with a quick-access menu for toggling instructions, toggling tools, or running auto-configure.
- **Toggle Menus** — Multi-select QuickPick menus for instructions and tools with category grouping, category-level toggling, and Select All / Clear All buttons.
- **Per-Instruction Disable** — Browse any instruction file in a virtual document, then use CodeLens actions to disable or re-enable individual rules without turning off the entire file. Disabled instructions are dimmed, tagged `[DISABLED]`, and excluded from Copilot's context.
- **Export** — Copy instruction files to `.github/instructions/` for team sharing. Exported instructions are automatically removed from the Toggle, Browse, and Export menus — they reappear if the exported file is deleted.
- **Multi-Window Safe** — Per-workspace staging directories with hash-based isolation and automatic cleanup of stale directories older than one hour.
- **Diagnostics** — Parses every instruction file on activation and logs warnings (missing IDs, duplicate IDs, malformed IDs) to the SharpPilot Output channel.
