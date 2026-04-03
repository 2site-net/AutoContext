## Chat Instructions

SharpPilot ships with curated instruction files organized into categories:

| Category | Coverage |
|----------|----------|
| **General** | Cross-cutting guidelines — code review, design principles, Docker, SQL, and more |
| **.NET** | C#, F#, VB.NET, ASP.NET Core, EF Core, testing frameworks, database drivers, and more |
| **Git** | Commit format |
| **Scripting** | PowerShell, Bash, Batch |
| **Web** | TypeScript, JavaScript, React, Angular, Vue, Svelte, Next.js, Node.js, testing frameworks, and more |

When enabled, instructions are automatically attached to every Copilot Chat conversation — so Copilot follows your coding standards without being told each time. Each instruction only activates when the matching technology is detected in your workspace (e.g., `.csproj` for C#, `react` in `package.json` for React, `.git` for commit format).

### Toggle instructions

Open the multi-select menu to control which instruction sets are active. Instructions are grouped by category — toggling a category header selects or deselects all items in that group. Use **Select All** or **Clear All** for bulk changes.

[Toggle Instructions](command:sharppilot.toggleInstructions)

### Exported instructions

If you have instruction files in `.github/instructions/` that match a built-in instruction by filename, those instructions are automatically hidden from the Toggle, Browse, and Export menus. The exported workspace-level file takes precedence. Delete the exported file to bring the built-in instruction back into those menus.
