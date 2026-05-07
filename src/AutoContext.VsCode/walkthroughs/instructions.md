## Chat Instructions

AutoContext ships with curated instruction files organized into categories:

| Category | Coverage |
|----------|----------|
| **General** | Cross-cutting guidelines — code review, design principles, REST API design, testing |
| **Languages** | Bash, Batch (CMD), C, C#, C++, CSS, Dart, F#, Go, GraphQL, Groovy, HTML, Java, JavaScript, Kotlin, Lua, PHP, PowerShell, Python, Ruby, Rust, Scala, SQL, Swift, TypeScript, VB.NET, YAML |
| **.NET** | ASP.NET Core, Blazor, EF Core, WPF, xUnit, NUnit, NuGet, database drivers, and more |
| **Web** | Angular, React, Vue, Svelte, Next.js, Node.js, Jest, Vitest, Cypress, Playwright, and more |
| **Tools** | Git commit format, Docker |

When enabled, instructions are automatically attached to every Copilot Chat conversation — so Copilot follows your coding standards without being told each time. Each instruction only activates when the matching technology is detected in your workspace (e.g., `.csproj` for C#, `react` in `package.json` for React, `.git` for commit format).

### Toggle instructions

Use the AutoContext sidebar to control which instruction sets are active. Instructions are grouped by category — use the inline actions on each item to enable or disable it. Use the `…` menu on the panel header to show or hide items that are not detected in your workspace.

[Open Instructions Panel](command:autocontext.instructions-view.focus)

### Exported instructions

If you have instruction files in `.github/instructions/` that match a built-in instruction by filename, those instructions appear as **overridden** in the panel with a distinct icon. Clicking an overridden item opens the workspace-level file for editing. Use the **Show Original** action to view the built-in version, or **Delete Override** to remove the workspace file and revert to the built-in version.

### Discovery tools for Copilot

Beyond the always-attached files, the extension contributes four Language Model tools so Copilot can **pull** the right rules on demand from inside an agent loop:

- `list_autocontext_instructions_files` — *"which curated rules apply to this path?"*
- `search_autocontext_instructions_files_by_metadata` — *"which rules have a Security section?"*
- `search_autocontext_instructions_files_by_content` — free-text body search across the catalogue
- `get_autocontext_instructions_file` — fetch the full body of a file, or only specific sections

These tools honour your toggles: disabled files are reported as such, and disabled rules are stripped from any returned body.
