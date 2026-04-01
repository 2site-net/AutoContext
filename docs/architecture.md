# Architecture

## What Is SharpPilot?

SharpPilot is a **quality assurance layer** that sits between GitHub Copilot and the developer's workspace. It provides two things: curated instruction files that shape how Copilot writes and reviews code, and MCP tool checks that Copilot can invoke to validate code against concrete rules. The extension owns the configuration and context; the MCP servers own the analysis.

## Design Philosophy

### Why Instructions + Tools?

Instructions alone give guidance but can't verify compliance. Tools alone can flag violations but without context they produce generic advice. Combining both means Copilot receives coding guidelines (instructions) and can then verify its own output against those guidelines (tools) — a feedback loop that catches mistakes before they leave the chat.

### Why EditorConfig-Driven Enforcement?

Style rules vary between projects. Rather than hardcoding one opinion, checkers read `.editorconfig` properties and enforce whichever direction the project specifies. If a project uses tabs, the checker enforces tabs. If it uses spaces, it enforces spaces. Instructions provide sensible defaults, but EditorConfig always wins — so a team's existing configuration is never contradicted.

### Why a Separate Workspace Server?

SharpPilot runs multiple MCP servers (one per tool category). Each server needs `.editorconfig` resolution, and parsing the full directory tree on every tool call would be wasteful. A single `SharpPilot.WorkspaceServer` process runs as a named-pipe server, shared by all MCP servers. This avoids duplicate parsing and keeps the checkers stateless.

### Why Per-Instruction Disable?

A single instruction file may contain dozens of rules. Turning off the entire file because one rule conflicts with a project convention defeats the purpose. Per-instruction disable (via `.sharppilot.json`) removes individual bullets from the normalized output so Copilot never sees them — without affecting the rest of the file.

---

## How It All Connects

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           VS Code Activation                                 │
│                                                                              │
│   Extension starts, spawns the workspace server, scans the workspace,       │
│   sets context keys, writes tool status and normalized instructions.          │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         1. WORKSPACE DETECTION                               │
│                                                                              │
│   WorkspaceContextDetector scans for .csproj, package.json, .git, etc.       │
│   Sets boolean context keys: hasDotNet, hasGit, hasTypeScript, …             │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        2. SERVER REGISTRATION                                │
│                                                                              │
│   One MCP server per category (dotnet, git, editorconfig, typescript).       │
│   A server is only registered when its context key is true AND at least      │
│   one of its tools is enabled in settings.                                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       3. INSTRUCTION INJECTION                               │
│                                                                              │
│   Instruction files are conditionally injected into Copilot's context        │
│   based on the same context keys. Per-instruction disable removes            │
│   individual rules without turning off the entire file.                      │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        4. TOOL CONFIGURATION                                 │
│                                                                              │
│   ToolsStatusWriter reads VS Code settings and writes disabled tool names    │
│   to .sharppilot.json. MCP servers read this file at runtime. Disabled       │
│   sub-checks are skipped unless they have EditorConfig backing (see below).  │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      5. RUNTIME (Copilot invokes tools)                      │
│                                                                              │
│   Copilot calls check_csharp_all / check_nuget_hygiene / check_git_all /    │
│   check_typescript_all / get_editorconfig. EditorConfig properties are       │
│   resolved on demand via the workspace server and drive checker              │
│   behavior — e.g., enforcement direction for brace and namespace style.      │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Activation Flow

When the extension activates, the following steps execute synchronously:

1. **`WorkspaceServerManager.start()`** — spawns the `SharpPilot.WorkspaceServer` service process, a standalone named-pipe server that resolves `.editorconfig` properties. Once ready, the pipe name is injected into every MCP server definition via `--workspace-server` so all servers share a single process.
2. **`ToolsStatusWriter.write()`** — reads VS Code settings, writes disabled tool names to `.sharppilot.json` in the workspace root.
3. **`WorkspaceContextDetector.detect()`** — scans the workspace for project files, `package.json` dependencies, and directory markers. Sets VS Code context keys that control both server registration and instruction injection.
4. **`ConfigManager.removeOrphanedIds()`** — cleans disabled-instruction IDs from `.sharppilot.json` that no longer match any instruction in the current extension version.
5. **`InstructionsWriter.removeOrphanedStagingDirs()`** — deletes per-workspace staging directories older than one hour that belong to other VS Code windows.
6. **`InstructionsWriter.write()`** — normalizes all instruction files into `instructions/.generated/`, stripping `[INSTxxxx]` tag identifiers and removing any individually disabled instruction bullets. Copilot always reads from the normalized output, so neither tags nor disabled content are visible to the model.
7. **`logDiagnostics()`** — parses every instruction file and logs warnings (e.g., missing `[INSTxxxx]` IDs) to the **SharpPilot** Output channel.

## Runtime Flow

When Copilot invokes an MCP tool (e.g., `check_csharp_all`):

1. The MCP server reads `.sharppilot.json` to determine which sub-checks are disabled.
2. If `editorConfigFilePath` is provided, the server queries the `SharpPilot.WorkspaceServer` service over the named pipe to resolve `.editorconfig` properties and merges them into the checker data.
3. **Disabled sub-checks with EditorConfig backing** — When a sub-check is disabled but implements `IEditorConfigFilter` *and* the resolved `.editorconfig` contains at least one key that checker consumes, the checker still runs in a restricted mode: it enforces only the EditorConfig-backed rules and skips its instruction-only (INST) checks. This lets project-level `.editorconfig` settings remain enforced even after a team opts out of the instruction.
4. **Fully disabled sub-checks** — When a sub-check is disabled and no relevant EditorConfig key is present, it is skipped entirely.
5. Enabled checkers read the merged EditorConfig values and use them to **drive** their enforcement direction — not just to skip conflicting checks.
6. The checker returns a report (✅ pass or ❌ violations found).

---

## Precedence

When multiple sources disagree, the following precedence applies:

| Priority | Source | Role |
|----------|--------|------|
| 1 | `.editorconfig` | Drives enforcement direction — checkers enforce whatever EditorConfig says. Instruction defaults yield to EditorConfig values. |
| 2 | Instruction files | Provide default coding guidance. Style rules in instructions are fallback defaults, not absolutes. |
| 3 | VS Code settings / `.sharppilot.json` | Control which tools and instructions are active. |
| 4 | Workspace context | Determines which servers and instructions are registered at all. |

See the "EditorConfig wins" rule in `copilot.instructions.md` for the user-facing statement of this precedence.

### EditorConfig as a Floor

Disabling a tool removes its **instruction-only** checks from the report, but project-level `.editorconfig` settings are a stronger signal than a personal tool toggle. Checkers that implement `IEditorConfigFilter` declare which EditorConfig keys they consume. When a checker is disabled but the resolved `.editorconfig` contains at least one of those keys, the checker runs in a restricted mode that enforces only the EditorConfig-backed rules and skips the instruction-only (INST) checks.

This means a team can commit a `.editorconfig` that requires file-scoped namespaces or braces on single-line blocks, and those checks remain enforced even if an individual developer disables the corresponding tool. The `.editorconfig` acts as a floor that cannot be silenced by local tool toggles.

Checkers with EditorConfig backing today:

| Checker | EditorConfig Keys |
|---------|-------------------|
| `check_csharp_coding_style` | `csharp_prefer_braces`, `dotnet_sort_system_directives_first`, `csharp_style_expression_bodied_methods`, `csharp_style_expression_bodied_properties` |
| `check_csharp_project_structure` | `csharp_style_namespace_declarations` |

---

## Instructions

SharpPilot ships curated Markdown instruction files organized into categories — .NET (C#, F#, VB.NET, ASP.NET Core, Blazor, EF Core, xUnit, …), Web (TypeScript, React, Angular, Vue, Next.js, Node.js, …), Scripting (PowerShell, Bash, Batch), Git, and General (Docker, REST API design, SQL, …). One always-on file (`copilot.instructions.md`) provides cross-cutting rules; the rest are toggleable.

Instructions are **workspace-aware** — they are only injected into Copilot's context when the workspace contains their technology (e.g., .NET instructions require a `.csproj` or `.sln` file). The always-on `copilot.instructions.md` is the only file that is attached unconditionally.

### Toggling

A multi-select QuickPick menu (`Toggle Instructions`) groups instructions by category and supports category-level toggling, Select All, and Clear All. Toggling an instruction off sets its VS Code setting (e.g., `sharppilot.instructions.dotnet.csharp`) to `false`, and the activation flow excludes it from the normalized output.

### Per-Instruction Disable

Each instruction file can contain dozens of individual rules. The `Browse Instructions` command opens a file in a virtual document where every rule is visible. CodeLens actions on each rule let you disable or re-enable it without turning off the entire file. Disabled rules are dimmed, tagged `[DISABLED]`, and written to `.sharppilot.json`. The normalization step strips them from Copilot's context entirely.

### Export

The `Export Instructions` command copies instruction files to `.github/instructions/` for team sharing via source control. Exported files are automatically removed from the Toggle, Browse, and Export menus — they reappear if the exported file is deleted.

### Normalization Pipeline

Copilot never reads the raw instruction files. Three directories form a write-through pipeline:

- **`instructions/`** — the authored source files. Each rule is tagged with an `[INSTxxxx]` identifier used for per-rule disable and CodeLens UI. These files are never served to Copilot directly.
- **`instructions/.workspaces/<hash>/`** — per-workspace staging. Each VS Code window writes its own normalized copy here, keyed by a SHA-256 hash of the workspace root path. Normalization strips `[INSTxxxx]` tags and removes disabled rules entirely. The staging layer exists because multiple VS Code windows share a single extension directory — without it, windows with different configurations would overwrite each other's output. Orphaned staging directories (from closed windows, older than one hour) are garbage-collected on activation.
- **`instructions/.generated/`** — the live output that Copilot's `chatInstructions` reads. After staging, files are promoted here with a content-comparison guard (`copyIfChanged`) so identical content is never rewritten. Each file has a `when` clause that combines the instruction's VS Code setting toggle and the workspace context key — Copilot only sees files relevant to the current workspace.

On activation (and on configuration or window-focus changes), `InstructionsWriter.write()` runs the full source → staging → promotion cycle. Content-comparison guards at both stages make re-runs essentially free when nothing changed.

> **Future:** The three-directory pipeline exists because VS Code's `chatInstructions` contribution point is static — it can only reference files on disk. If the `chatPromptFiles` proposed API graduates to stable, `registerInstructionsProvider()` could serve normalized instruction content in-memory, eliminating the staging and generated directories entirely. Each window would provide its own content dynamically with no multi-window file conflicts. See [docs/future/dynamic-editorconfig-instructions.md](future/dynamic-editorconfig-instructions.md) for the current status of that API.

---

## MCP and Tools

SharpPilot exposes four tool categories across two MCP servers. The VS Code extension registers them as `--scope dotnet`, `--scope git`, `--scope editorconfig`, and `--scope typescript` — so each category appears in a separate section in the tools UI.

Servers are workspace-aware: the extension only registers a server when the workspace contains matching content (e.g., `.csproj` files for .NET, `.git` directory for Git). The EditorConfig server is always available regardless of workspace content. If all sub-checks for a category are disabled in settings, that server is not registered at all.

Most tools Copilot sees are **aggregation tools** — a single MCP tool that loops over multiple sub-checks internally. Each sub-check can be individually toggled on or off in VS Code settings (e.g., `sharppilot.tools.check_csharp_naming_conventions`). When a sub-check is disabled, the aggregation tool simply skips it, so the report only contains the checks the team cares about.

### SharpPilot: DotNet

An aggregation tool (`check_csharp_all`) that bundles C# sub-checks, plus a standalone tool (`check_nuget_hygiene`).

| Tool | Sub-check | Purpose |
|------|-----------|---------|
| `check_csharp_all` | `check_csharp_coding_style` | Brace style, namespace style, formatting, and general C# idioms. |
| | `check_csharp_member_ordering` | Fields → constructors → properties → methods and similar ordering rules. |
| | `check_csharp_naming_conventions` | PascalCase types, camelCase locals, `_camelCase` fields, `I`-prefixed interfaces, etc. |
| | `check_csharp_async_patterns` | Async/await correctness — `ConfigureAwait`, `Async` suffix, fire-and-forget guards. |
| | `check_csharp_nullable_context` | `<Nullable>enable</Nullable>`, null guards, `!` suppressions. |
| | `check_csharp_project_structure` | Project file hygiene — target framework, implicit usings, output paths. |
| | `check_csharp_test_style` | Test naming, `Fact`/`Theory` usage, assertion style, arrange-act-assert. |
| `check_nuget_hygiene` | `check_nuget_hygiene` | No duplicate, floating, or wildcard package versions; no missing `Version` attribute (unless Central Package Management is enabled); flags packages with built-in .NET alternatives. |

When an `.editorconfig` path is provided, `check_csharp_all` resolves its properties and uses them to drive checker behavior (e.g., brace and namespace style enforcement direction).

### SharpPilot: Git

An aggregation tool that bundles sub-checks for git commit quality.

| Tool | Sub-check | Purpose |
|------|-----------|---------|
| `check_git_all` | `check_git_commit_format` | Conventional Commits structure — type, scope, subject line length, footer format. |
| | `check_git_commit_content` | Commit content best practices — atomic changes, meaningful messages, body/footer usage. |

### SharpPilot: EditorConfig

One standalone tool (not an aggregation — no sub-checks to toggle).

| Tool | Purpose |
|------|---------|
| `get_editorconfig` | Resolves the effective `.editorconfig` properties for a given file path — walks the directory tree, evaluates glob patterns and section cascading, and returns the final key-value pairs. |

> **Future:** The current design relies on Copilot calling `get_editorconfig` explicitly — which depends on the model following the instruction in `copilot.instructions.md`. A planned improvement would replace this with a dynamic `InstructionsProvider` that injects `.editorconfig` rules into the chat context automatically, removing the tool-call dependency. This is blocked on the VS Code `chatPromptFiles` proposed API graduating to stable. See [docs/future/dynamic-editorconfig-instructions.md](future/dynamic-editorconfig-instructions.md) for details.

### SharpPilot: TypeScript

An aggregation tool for TypeScript quality.

| Tool | Sub-check | Purpose |
|------|-----------|---------|
| `check_typescript_all` | `check_typescript_coding_style` | Flags `any`, enums, `@ts-ignore`, `Function`/`Object` types, and other common anti-patterns. When an `.editorconfig` path is provided, resolves its properties and uses them to drive checker behavior. |

### Viewing Tool Invocation Logs

Each server logs tool invocations (tool name, content length, data keys) to stderr, which VS Code surfaces in the **Output** panel. To view the logs:

1. Open the **Output** panel (`Ctrl+Shift+U`).
2. Select the server from the dropdown (e.g., *SharpPilot: DotNet*).

Only SharpPilot log messages are emitted — host and framework noise is filtered out.

---

*This document provides an architectural overview. For build instructions and configuration, see the [README](../README.md).*
