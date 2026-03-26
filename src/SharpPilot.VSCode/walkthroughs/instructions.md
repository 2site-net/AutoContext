## Chat Instructions

SharpPilot ships with 60 curated instruction files organized into categories:

| Category | Examples |
|----------|----------|
| **General** | Copilot behavior, code review, design principles, Docker, GraphQL, REST API design, SQL |
| **.NET** | ASP.NET Core, async/await, Blazor, C#, coding standards, Dapper, Entity Framework Core, F#, gRPC, MAUI, NuGet, performance, Redis, SignalR, testing (xUnit, MSTest, NUnit), Unity, VB.NET, WPF, WinForms |
| **Git** | Commit format |
| **Scripting** | PowerShell, Bash, Batch |
| **Web** | TypeScript, HTML/CSS, JavaScript, React, Angular, Vue, Svelte, Next.js, Node.js, testing (Vitest, Jest, Jasmine, Mocha, Playwright, Cypress) |

When enabled, instructions are automatically attached to every Copilot Chat conversation — so Copilot follows your coding standards without being told each time. Each instruction only activates when the matching technology is detected in your workspace (e.g., `.csproj` for C#, `react` in `package.json` for React, `.git` for commit format).

### Toggle instructions

Open the multi-select menu to control which instruction sets are active. Instructions are grouped by category — toggling a category header selects or deselects all items in that group. Use **Select All** or **Clear All** for bulk changes.

[Toggle Instructions](command:sharppilot.toggleInstructions)

### Overrides

If you have instruction files in `.github/instructions/` that match a built-in instruction by filename, SharpPilot detects them as **overrides**. Overridden instructions appear with a $(file-symlink-directory) badge in the toggle menu, signaling that a local version is in use. The built-in instruction still activates normally — the badge is a visual cue so you know a workspace-level file exists.