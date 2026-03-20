## Chat Instructions

SharpPilot ships with 57 curated instruction files organized into categories:

| Category | Examples |
|----------|----------|
| **General** | Copilot behavior, code review, design principles |
| **.NET** | C# coding style, F# coding style, VB.NET coding style, async/await, coding standards, NuGet, debugging, performance |
| **Language-specific** | Blazor, WPF, WinForms, MAUI, Unity |
| **Git** | Commit format |
| **Web & Frameworks** | TypeScript, HTML/CSS, React, Angular, Vue, Svelte, Next.js, GraphQL |
| **Web Testing** | Vitest, Jest, Jasmine, Mocha, Playwright, Cypress |

When enabled, instructions are automatically attached to every Copilot Chat conversation — so Copilot follows your coding standards without being told each time. Each instruction only activates when the matching technology is detected in your workspace (e.g., `.csproj` for C#, `react` in `package.json` for React, `.git` for commit format).

### Toggle instructions

Open the multi-select menu to control which instruction sets are active. Instructions are grouped by category — toggling a category header selects or deselects all items in that group. Use **Select All** or **Clear All** for bulk changes.

[Toggle Instructions](command:sharp-pilot.toggleInstructions)

### Overrides

If you have instruction files in `.github/instructions/` or a `.github/copilot-instructions.md` in your workspace, SharpPilot detects them as **overrides** of the built-in versions. Overridden instructions appear with a $(file-symlink-directory) badge in the toggle menu, signaling that a local version is in use. The built-in instruction still activates normally — the badge is a visual cue so you know a workspace-level file exists.
