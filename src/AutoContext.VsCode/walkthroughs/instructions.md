## Chat Instructions

AutoContext ships with curated instruction files organized into categories:

| Category | Coverage |
|----------|----------|
| **General** | Cross-cutting guidelines — code review, design principles, REST API design, testing |
| **Languages** | Bash, Batch (CMD), C, C#, C++, CSS, Dart, F#, Go, GraphQL, Groovy, HTML, Java, JavaScript, Kotlin, Lua, PHP, PowerShell, Python, Ruby, Rust, Scala, SQL, Swift, TypeScript, VB.NET, YAML |
| **.NET** | ASP.NET Core, Blazor, EF Core, WPF, xUnit, NUnit, NuGet, database drivers, and more |
| **Web** | Angular, React, Vue, Svelte, Next.js, Node.js, Jest, Vitest, Cypress, Playwright, and more |
| **Tools** | Git commit format, Docker |

When enabled, guidance is attached to every chat conversation — so your agent follows your coding standards without being told each time. Each one only switches on when the matching technology is present in your project.

### Toggle instructions

Use the AutoContext sidebar to control which instruction sets are active. Instructions are grouped by category — use the inline actions on each item to enable or disable it. Use the `…` menu on the panel header to show or hide items that are not detected in your workspace.

[Open Instructions Panel](command:autocontext.instructions-view.focus)

### Exported instructions

If you have instruction files in `.github/instructions/` that match a built-in instruction by filename, those instructions appear as **overridden** in the panel with a distinct icon. Clicking an overridden item opens the workspace-level file for editing. Use the **Show Original** action to view the built-in version, or **Delete Override** to remove the workspace file and revert to the built-in version.

### Your agent can look things up

Beyond the guidance that's always attached, your agent can search the full
catalogue on demand — to find the rules that apply to a particular file, or
to answer a topical question like *"what does this project say about error
handling?"*

This respects your choices: guidance you've switched off is never returned.
