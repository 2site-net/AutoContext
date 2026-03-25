## Welcome to SharpPilot

SharpPilot is a quality-assurance companion for GitHub Copilot. It ships curated **chat instructions** that guide Copilot's responses and registers **MCP servers** whose tools let Copilot check your code against best practices — all configurable from a single status-bar entry point.

### What you get

- **60 Chat Instructions** — Markdown guidelines covering .NET, C#, F#, VB.NET, TypeScript, testing frameworks, Git, REST APIs, scripting, and more. Active instructions are automatically attached to every Copilot Chat conversation so Copilot follows your coding standards without being told each time.
- **11 MCP Tools** — Model-invokable checks for C# coding style, naming conventions, async patterns, member ordering, nullable context, NuGet hygiene, project structure, test style, and Git commit format and content. Copilot can call these tools in agent mode to inspect your code on the spot.
- **Status Bar** — A single indicator showing how many instructions and tools are active (`$(book) X/Y $(tools) X/Y`). Click it to toggle instructions, toggle tools, or auto-configure.
- **Auto Configuration** — Workspace detection scans `.csproj`, `package.json`, `.git`, NuGet packages, and npm dependencies, then enables only the items relevant to your project.
- **Export & Version Tracking** — Export instruction files to `.github/instructions/` for team sharing. A manifest tracks exported file hashes so the extension can alert you when instructions are updated in a new release.
- **Override Detection** — When instruction files exist in `.github/instructions/` or `.github/copilot-instructions.md`, SharpPilot detects them as overrides and marks them with a badge in the toggle menu.
