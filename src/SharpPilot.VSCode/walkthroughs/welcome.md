## Welcome to SharpPilot

SharpPilot is a quality-assurance companion for GitHub Copilot. It ships curated **chat instructions** that guide Copilot's responses and registers **MCP servers** whose tools let Copilot check your code against best practices — all configurable from a single status-bar entry point.

### What you get

- **60 Chat Instructions** — Markdown guidelines covering .NET, C#, F#, VB.NET, TypeScript, testing frameworks, Git, REST APIs, scripting, and more. Active instructions are automatically attached to every Copilot Chat conversation so Copilot follows your coding standards without being told each time.
- **11 MCP Tools** — Model-invokable checks for C# coding style, naming conventions, async patterns, member ordering, nullable context, NuGet hygiene, project structure, test style, and Git commit format and content. Copilot can call these tools in agent mode to inspect your code on the spot.
- **Status Bar** — A single indicator showing how many instructions and tools are active (`$(book) X/Y $(tools) X/Y`). Click it to toggle instructions, toggle tools, or auto-configure.
- **Auto Configuration** — Workspace detection scans `.csproj`, `package.json`, `.git`, NuGet packages, and npm dependencies, then enables only the items relevant to your project.
- **Export** — Export instruction files to `.github/instructions/` for team sharing. Exported instructions are automatically removed from the Toggle, Browse, and Export menus.
- **Per-Instruction Disable** — Browse any instruction file and use CodeLens actions to disable or re-enable individual instructions without turning off the entire file.
