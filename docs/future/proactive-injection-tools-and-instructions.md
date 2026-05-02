# Proactive Injection: Surface AutoContext Tools and `applyTo` Instructions

> **Status:** Future / design note
>
> Two related recommendations about how AutoContext customizations (MCP tools and instruction files) integrate with the Copilot/agent loop in VS Code. Both stem from the same underlying critique: AutoContext currently relies too heavily on the model's initiative to discover its own customizations. Discoverability is fine for optional/situational integrations; enforcement requires presence in the prompt.

---

## 1. Stop deferring high-value AutoContext MCP tools

### Background: what "deferred" means

In an agent session, VS Code surfaces some MCP tools in an `<availableDeferredTools>` block instead of the default tool list. Examples from AutoContext today:

- `mcp_autocontext_d_check_csharp_all`
- `mcp_autocontext_d_check_nuget_hygiene`
- `mcp_autocontext_e_get_editorconfig`
- `mcp_autocontext_g_check_git_all`
- `mcp_autocontext_t_check_typescript_all`

Deferred tools are not directly callable. The model must:

1. Realize a tool *might* exist for the task.
2. Call `tool_search` with a natural-language query.
3. Wait for the search to return matching tool definitions.
4. *Then* call the tool.

The mechanism exists because VS Code can't dump every MCP tool from every server into the prompt — it would blow the context window. So lesser-used tools sit behind a search.

### The problem: a discoverability tax

- The model has to *guess* that a relevant tool might exist before searching. If it doesn't think to search, it never finds the tool — even when the tool would have been the correct choice.
- For tools the user wants invoked routinely (e.g. `check_csharp_all` before declaring a C# task done, or `get_editorconfig` before editing), this guess-first model is unreliable. The model will frequently skip them and fall back to generic alternatives (raw `dotnet build`, manual file inspection, etc.).
- Compare to "always-available" tools like `read_file` or `grep_search`: those are in the default list, so the model uses them naturally without needing prior knowledge.

### The recommendation

Split MCP tools into two tiers based on usage frequency:

| Tier | Examples | Mechanism |
|------|----------|-----------|
| **Always-available** (default tool list) | AutoContext's own `check_*`, `get_editorconfig` — tools you want invoked on every relevant task | Inject directly into the prompt's tool definitions |
| **Deferred** (behind `tool_search`) | GitHub, Playwright, Microsoft Docs, container tools — niche/situational integrations | Keep behind search, since they're not on the critical path |

**First-party AutoContext tools shouldn't pay the same discoverability cost as third-party integrations.** They represent the workflow we're trying to enforce, so they need to be top-of-mind for the model — not buried.

### Practical implication

If the AutoContext VS Code extension controls how its MCP tools are registered with Copilot, the high-value ones (gating quality checks, `editorconfig` lookups) should be marked as non-deferred — either by registering them differently with the MCP host, or by hinting to VS Code that they belong in the default tool surface.

---

## 2. Make `applyTo` actually inject, not just filter

### Background: how `applyTo` works today

Instruction files use frontmatter like:

```yaml
applyTo: **/*.cs
```

In **VS Code's editor experience**, `applyTo` works as a *filter*: when the user is editing `Foo.cs`, the editor can show "this instruction file applies." It's essentially a passive matcher — useful for display/scoping.

Then there's `copilot-instructions.md`, which has different behavior: it's **always auto-attached** to every Copilot prompt, regardless of context. No `applyTo` needed — it just gets injected.

### The problem: passive filtering in an active loop

In an agent loop (Copilot Chat agent mode, where the model iteratively reads files, edits them, runs tools), the relationship between "what file is in context" and "what instructions apply" should be active, not passive:

- If the agent reads `Foo.cs`, the matching `lang-csharp.instructions.md` should be **auto-injected** into the prompt right then, the same way `copilot-instructions.md` always is.
- Today's behavior: `applyTo` is just a filter for display/scoping. The agent doesn't get the C# instructions auto-attached when it touches a `.cs` file — it has to know they exist, find them, and read them itself.

You can observe this in agent sessions: the system lists all instruction files with their `applyTo` patterns, but the *contents* aren't loaded. A note tells the model to "use the `read_file` tool to read it before proceeding." That's exactly the gap — the model has to remember to read them.

### The recommendation

Promote `applyTo` from a filter to an **injection rule**:

- When a file matching `applyTo: **/*.cs` enters the agent's context (open, read, or edited), the corresponding instruction file gets prepended to the prompt automatically.
- The model never has to "decide" to load it — it's just there, like `copilot-instructions.md`.
- This makes the instruction reliably enforced rather than aspirationally available.

### Why this matters for AutoContext specifically

AutoContext ships many language- and framework-specific instruction files (`lang-csharp`, `dotnet-async-await`, `web-vitest`, etc.). They only deliver value if the model actually reads them at the right moment. Passive `applyTo` filtering means many of them are silently skipped during agent runs. Active injection — "this file is in context → these instructions are in context" — closes that gap.

If the VS Code extension already auto-injects some files but not `applyTo` matches, that's a gap worth closing.

---

## Common thread

Both recommendations attack the same root cause: **AutoContext relies too heavily on the model's initiative to discover its own customizations**. Tools sit behind search; instructions sit behind filters. In a hands-off agent loop, "discoverable" isn't enough — anything we actually want enforced needs to be **proactively injected** into the prompt at the moment it's relevant.

- Discoverability → fine for optional/situational stuff.
- Enforcement → requires presence.

---

# Execution Plan

> Based on a survey of the current extension code and the VS Code 1.100+ APIs available to us. The two features sit on different sides of the platform line: tool surfacing is largely under our control via the `LanguageModelTool` API; instruction injection is mostly controlled by VS Code/Copilot, so our work is partially upstream + workaround.

## Current state (recap, with file pointers)

- **MCP surface**: A single [`McpServerProvider`](../../src/AutoContext.VsCode/src/mcp-server-provider.ts) registers one `vscode.McpStdioServerDefinition` via `contributes.mcpServerDefinitionProviders`. The .NET server ([`AutoContext.Mcp.Server`](../../src/AutoContext.Mcp.Server/Program.cs)) declares **all** tools to the MCP SDK at startup; the resulting tool count (20+) is likely what trips Copilot's deferral heuristic and pushes them behind `tool_search`.
- **Instructions surface**: `contributes.chatInstructions` is auto-generated from [`instructions-files.json`](../../src/AutoContext.VsCode/resources/instructions-files.json) by [`package-instructions-manifest-generator.ts`](../../src/AutoContext.VsCode/src/package-instructions-manifest-generator.ts). Each entry's `when` clause gates by config flag + workspace flag + override flag — but **not** by the file currently in agent context. `applyTo` semantics inside the instruction frontmatter are interpreted by VS Code/Copilot, not by us.
- **Workspace detection**: [`workspace-context-detector.ts`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) populates `autocontext.workspace.*` context keys at activation. We already have the plumbing to react to context changes — we just don't currently react to *editor*-level changes.

## Feature 1 — Surface high-value MCP tools as first-class language-model tools

**Goal**: Make tools like `check_csharp_all`, `check_typescript_all`, `check_nuget_hygiene`, `check_git_all`, `get_editorconfig` appear in Copilot's default tool list instead of behind `tool_search`.

### Strategy

VS Code 1.95+ ships the `contributes.languageModelTools` contribution + `vscode.lm.registerTool()` API. Tools registered this way are **first-class chat tools**: they appear in the always-available tool list, can be `#`-referenced by name, and are not subject to the same deferred-by-count heuristic as MCP tools.

We keep the .NET MCP server as the execution engine (tool implementations stay where they are), and add a thin TypeScript shim that registers a small, curated set of LM tools that delegate to the MCP server over the existing pipe.

### Manifest changes

1. Add a `surface` field to each entry in [`mcp-tools.json`](../../src/AutoContext.VsCode/resources/mcp-tools.json):
   - `"surface": "lm-tool"` → registered as a first-class `LanguageModelTool` (always-available)
   - `"surface": "mcp"` → stays in the MCP server only (current behaviour, deferred)
2. Curate the `lm-tool` set conservatively. Initial proposal:
   - `check_csharp_all`, `check_typescript_all`, `check_nuget_hygiene`, `check_git_all`
   - `get_editorconfig`
   - Everything else stays `mcp` until proven necessary. Keeping the always-available list small (≤ ~8) is critical to avoid eating context.
3. Update [`mcp-tools-manifest.ts`](../../src/AutoContext.VsCode/src/mcp-tools-manifest.ts) and the loader to expose the new field.

### Code changes

1. **New file** `src/lm-tool-registrar.ts`:
   - On activation, iterate manifest entries with `surface === "lm-tool"`.
   - For each, call `vscode.lm.registerTool(name, { invoke, prepareInvocation })`.
   - `invoke` forwards the request to the MCP server via the existing worker pipe (or a direct in-process call to the same dispatcher used by `ToolInvoker`).
   - Honour the same gating as today (config-enabled + workspace-flag match): if disabled, the tool either isn't registered or returns a structured "disabled" result.

2. **`package.json`** — auto-generate `contributes.languageModelTools` entries alongside `chatInstructions` (extend [`package-instructions-manifest-generator.ts`](../../src/AutoContext.VsCode/src/package-instructions-manifest-generator.ts) or add a sibling generator). Each entry needs:
   - `name`, `displayName`, `modelDescription`, `userDescription`
   - `canBeReferencedInPrompt: true`
   - `inputSchema` mirroring the MCP tool's parameter schema (sourced from [`mcp-workers-registry.json`](../../src/AutoContext.Mcp.Server/mcp-workers-registry.json))
   - `tags` (e.g. `["autocontext", "quality-gate"]`) for discoverability
   - A `when` clause matching the workspace flag(s) so the tool only shows up where it's relevant

3. **MCP server side** — when the same tool is registered as an LM tool in the extension, suppress its registration with the MCP SDK to avoid double-exposure. Add a `surface` field to [`mcp-workers-registry.json`](../../src/AutoContext.Mcp.Server/mcp-workers-registry.json) (or read the extension manifest at startup) and skip MCP `[Tool]` registration for `lm-tool` entries.

### Risks & decisions

- **Duplication risk**: A user with both surfaces enabled would see the same tool twice. Mitigation: enforce a single surface per tool at build time (validation in the manifest generator).
- **Capability parity**: LM tools have a different invocation contract than MCP tools (different result shapes, no streaming progress). Need to validate that `check_*` outputs render acceptably in Copilot Chat.
- **Tool budget**: VS Code/Copilot has a soft cap on always-available tools before they get deferred. Keep the curated set small; don't promote every tool.

### Acceptance

- `check_csharp_all` shows up under `#` references in Copilot Chat without invoking `tool_search`.
- The model invokes `get_editorconfig` automatically before edits in `.cs`/`.ts` files in workspaces where the corresponding workspace flag is set.
- The MCP server still works for the long-tail tools and for non-VS-Code MCP clients.

## Feature 2 — Active injection of `applyTo` instructions

**Goal**: When the agent is touching a `.cs` file, the matching instruction file lands in the prompt automatically — same way `copilot-instructions.md` does today.

### Reality check

VS Code/Copilot owns how `chatInstructions` entries with `applyTo` frontmatter get attached to the prompt. We cannot directly change that behaviour from an extension. The current evidence (instruction files listed but contents not loaded; the model is told to call `read_file`) suggests `applyTo` is treated as scoping metadata, not as an injection trigger.

So the plan splits into two tracks: a **pragmatic workaround** we can ship, and an **upstream ask** to fix the platform.

### Track A — Pragmatic workaround (shippable now)

Idea: drive instruction injection through the `when`-clause mechanism we already control, but bind it to **the active editor / files-in-context** rather than only to workspace-level flags.

1. **New context key**: `autocontext.editor.applyTo.{key}` — set per-instruction when the instruction's `applyTo` glob matches at least one document currently in scope.
2. **New module** `src/active-context-tracker.ts`:
   - Subscribe to `vscode.window.onDidChangeActiveTextEditor` and `vscode.workspace.onDidOpenTextDocument` (and the corresponding close event to clear stale matches).
   - For each in-scope document, evaluate the `applyTo` patterns from each instruction file's frontmatter (parsed already by [`instructions-file-parser.ts`](../../src/AutoContext.VsCode/src/instructions-file-parser.ts)) against the document URI.
   - Set / clear `autocontext.editor.applyTo.{key}` accordingly via `vscode.commands.executeCommand('setContext', ...)`.
3. **Generator update**: extend [`package-instructions-manifest-generator.ts`](../../src/AutoContext.VsCode/src/package-instructions-manifest-generator.ts) so each `chatInstructions` entry's `when` adds an OR-branch `|| autocontext.editor.applyTo.{key}`. That way the instruction is attached when **either** the workspace-flag matches **or** an in-scope file matches its `applyTo`.
4. **Scope policy**: define what "in scope" means. Conservative starting point: active editor + visible editors. More aggressive: all open editors. Avoid scanning the whole workspace — that defeats the purpose.

This isn't true per-turn injection (the agent loop doesn't necessarily re-evaluate `when` clauses every step), but it dramatically tightens the link between "what the user is looking at" and "what instructions get attached," which closes most of the gap.

### Track B — Upstream ask

File a VS Code / Copilot issue requesting that `chatInstructions` entries with `applyTo` patterns be **auto-attached when files matching the pattern are in the agent's reading/editing context** — not just at conversation start. Reference the existing `copilot-instructions.md` behaviour as the model. AutoContext's instruction set is a strong real-world driver for this feature.

### Optional — chat participant fallback

If Track A proves insufficient, a `@autocontext` chat participant could explicitly inject the relevant instruction text into responses, but this requires user opt-in (`@`-mention) and doesn't help in pure agent mode. Keep this as a fallback, not the primary path.

### Acceptance

- Opening `Foo.cs` causes `lang-csharp.instructions.md` to be attached to subsequent Copilot prompts in that workspace, even if `hasCSharp` workspace flag wasn't already triggering it.
- Switching to a `.ts` file swaps the attached instructions accordingly.
- No regression in the existing workspace-flag-based attachment.

## Implementation order

1. Feature 1, manifest + generator changes (additive, low risk).
2. Feature 1, `lm-tool-registrar.ts` for one tool (`get_editorconfig`) end-to-end as a proof of concept.
3. Feature 1, expand to the curated set; suppress MCP-side registration for promoted tools.
4. Feature 2 Track A, active-context tracker behind a feature flag (`autocontext.experimental.activeApplyTo`).
5. Feature 2 Track A, generator wiring + integration tests with workspace fixtures.
6. Feature 2 Track B, upstream issue filed in parallel (no code dependency).

## Out of scope (for now)

- Replacing the MCP server with pure LM tools — MCP gives us non-VS-Code clients and a richer protocol; we just want a curated subset surfaced.
- Modifying `.github/copilot-instructions.md` dynamically — too invasive, conflicts with user-owned content.
- Per-turn re-evaluation of `applyTo` (true active injection) — gated on upstream changes.

