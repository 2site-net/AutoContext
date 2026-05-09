# Plan — Agent-Plugin Discovery Enhancements

> **Status:** Draft. Builds on the bundled agent-plugin shipped with the
> VS Code extension (manifest at
> [src/AutoContext.VsCode/plugin/.claude-plugin/plugin.json](../../src/AutoContext.VsCode/plugin/.claude-plugin/plugin.json),
> hook config at
> [src/AutoContext.VsCode/plugin/hooks/hooks.json](../../src/AutoContext.VsCode/plugin/hooks/hooks.json),
> installer at
> [src/AutoContext.VsCode/src/agent-plugin-installer.ts](../../src/AutoContext.VsCode/src/agent-plugin-installer.ts)).
> Each capability below is gated and shippable independently — the plan
> sequences them by impact-vs-effort, not as a monolith.

## Context

Today the AutoContext agent plugin ships exactly one capability: a
`SessionStart` hook
([autocontext-session-start.cts](../../src/AutoContext.VsCode/src/hooks/autocontext-session-start.cts))
that injects the two
[ALWAYS_ATTACHED_INSTRUCTIONS_FILES](../../src/AutoContext.VsCode/src/always-attached-instructions-files.ts)
(`copilot.instructions.md`, `autocontext.instructions.md`) as
`additionalContext` so they prime every chat turn — including ones the
agent might otherwise treat as trivial. End-to-end smoke confirmed
this works: the model receives, acknowledges, and **acts on** the
injected rules in subsequent turns.

The Claude-format plugin spec
([anthropic plugin docs](https://docs.claude.com/en/api/agent-sdk/plugins))
exposes additional primitives our plugin manifest does not yet use:

| Primitive            | Today | Spec capability |
|----------------------|-------|-----------------|
| `hooks/`             | `SessionStart` only | Also `PreToolUse`, `PostToolUse`, `UserPromptSubmit`, `Stop`, `Notification`, `PreCompact`, `SubagentStop` |
| `commands/`          | none | Markdown files become `/<command>` slash commands |
| `agents/`            | none | Sub-agents with curated tool / instruction allow-lists |
| `mcpServers` (in `plugin.json`) | none — extension uses VS Code's `McpServerDefinitionProvider` | Plugin-declared MCP servers, portable to non-VS-Code Claude clients |
| Plugin-bundled instructions | sourced from `<extension>/instructions/` via `__dirname` walk | Could relocate to `<plugin>/instructions/` for portability |

The existing AutoContext discovery surfaces — both the four
`vscode.lm` tools registered by
[extension-registrations.ts](../../src/AutoContext.VsCode/src/extension-registrations.ts)
(`list_*`, `search_*_by_metadata`, `search_*_by_content`,
`get_autocontext_instructions_file`; documented in
[docs/architecture.md → Instruction Discovery](../architecture.md#instruction-discovery-lm-tools))
and the five MCP tools advertised by
[McpSdkAdapter.cs](../../src/AutoContext.Mcp.Server/Tools/McpSdkAdapter.cs)
from
[mcp-workers-registry.json](../../src/AutoContext.Mcp.Server/mcp-workers-registry.json)
— are **discoverable but not enforced**. The model has to *think* to
call them. This plan adds plugin-driven mechanisms that close the
gap between "rules exist" and "rules are mechanically applied".

## Goals

- Convert AutoContext from advisory rules → mechanically enforced
  validation, without changing user behaviour.
- Surface the existing LM / MCP tool catalogue under ergonomic
  slash-commands so users don't have to phrase the right prompt.
- Provide role-specific tool / instruction stacks via sub-agents.
- Keep every hook idempotent and best-effort: never block activation,
  the chat session, or a tool call. Match the safety stance of the
  existing `SessionStart` hook (read-only, swallow errors).
- Cross-IDE portability where it costs little (Claude Code / Desktop
  consuming the same plugin folder).

## Non-Goals

- Reimplementing existing tools as plugin commands. Slash commands
  are **wrappers** over the existing LM / MCP surfaces, not parallel
  implementations.
- Changing the `chat.pluginLocations` registration model (that's
  Phase 0 / already shipped).
- Adding new MCP tools. Hook → analyzer flows reuse the registry as
  it stands.
- Replacing `chatInstructions` in `package.json`. Both surfaces
  coexist (architecture-doc precedent).

## Phases

The phases are independent and each ships its own VSIX. Order is by
ROI; nothing later depends on something earlier landing first
unless flagged explicitly.

---

### Phase 1 — `PostToolUse` analyzer hook (highest ROI)

**One-line:** after the agent writes a file, the matching analyzer
runs automatically and the findings come back as `additionalContext`
for the next turn.

**Why:** today, `analyze_csharp_code` /
`analyze_typescript_code` / `analyze_nuget_references` /
`analyze_git_commit_message` exist on the MCP server but only fire
when the agent decides to invoke them. A hook makes validation
**inevitable**, not optional.

**Scope:**

- New hook script
  `src/AutoContext.VsCode/src/hooks/autocontext-post-tool-use.cts`
  (companion to the existing SessionStart script, same `.cts → .cjs`
  build pipeline already implemented in
  [build.ps1](../../build.ps1)).
- Triggered for tool names known to write or commit code. Initial
  matcher list (from the Claude tool naming convention used in the
  plugin spec): `Write`, `Edit`, `MultiEdit`, plus VS Code Copilot's
  equivalents (`replace_string_in_file`, `create_file`,
  `multi_replace_string_in_file`, `edit_notebook_file`). The hook
  reads the tool's `input` (file path + new content) from stdin per
  the spec's `PostToolUse` payload shape.
- Routing table from path → analyzer:
  - `**/*.cs` → MCP `analyze_csharp_code` (with `originalPath`,
    `comparedPath` / `projectDirectory` / `rootNamespace` for tests
    when detectable).
  - `**/*.{ts,tsx,mts,cts}` → MCP `analyze_typescript_code`.
  - `**/*.csproj` → MCP `analyze_nuget_references`.
  - Other paths → no-op (emit `{}` and exit 0).
- The hook **does not** call MCP directly. It emits a
  `additionalContext` block with the file path + a note instructing
  the agent to invoke the matching analyzer. Rationale: the MCP
  server lives behind a stdio pipe owned by the extension host, so
  forking a node script to talk to it would race the host's lifecycle
  and require duplicate spawn logic. Letting the model invoke its
  existing MCP tool is the correct seam.
  - Open question: if model latency is a concern, a future iteration
    can move analyzer dispatch into the hook by calling
    `AutoContext.Mcp.Server` over a long-lived UDS / named pipe
    advertised in `additionalContext` at SessionStart. Out of scope
    for Phase 1.

**Hook output shape (PostToolUse spec):**

```jsonc
{
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": "<!-- AutoContext PostToolUse: src/Foo/Bar.cs -->\n\nYou just wrote `src/Foo/Bar.cs`. Before declaring this turn done, call `analyze_csharp_code` with `content` = the new file body and `originalPath` = the file's absolute path. If findings are returned, fix them and re-validate."
    }
}
```

**Files touched:**

- `src/AutoContext.VsCode/plugin/hooks/hooks.json` — add `PostToolUse`
  entry alongside the existing `SessionStart`.
- `src/AutoContext.VsCode/src/hooks/autocontext-post-tool-use.cts` —
  new.
- [build.ps1](../../build.ps1) — extend the hook-staging block to
  copy `dist/hooks/autocontext-post-tool-use.cjs` into
  `plugin/scripts/`.
- `src/AutoContext.VsCode/.gitignore` — already covers
  `plugin/scripts/*.cjs`.

**Tests:**

- New unit test:
  `src/AutoContext.VsCode/tests/unit-tests/hooks/autocontext-post-tool-use.test.ts`
  driving the hook function-by-function (the extracted helpers
  `routeToolCall(toolName, input)` →
  `{ analyzer, params } | null` and `formatAdditionalContext(...)`).
- Smoke: feed each test path into `node
  plugin/scripts/autocontext-post-tool-use.cjs` and assert the
  emitted JSON.

**Risks:**

- Tool-name matchers drift as the host renames tools. Mitigation:
  central matcher list in the script; warn when a `Write`-class tool
  is observed without a known name (logged via stderr — VS Code
  surfaces hook stderr in the agent transcript).
- Spammy `additionalContext` if the agent edits many files in one
  turn. Mitigation: deduplicate by absolute path within the hook
  invocation (PostToolUse fires once per tool call, but a multi-edit
  call may carry several paths).

---

### Phase 2 — `commands/` slash-command surface

**One-line:** ship `/autocontext-*` slash commands that wrap the
existing LM / MCP tools so the user gets a one-shot lookup without
phrasing the right prompt.

**Why:** the four LM-instruction tools and the analyzer MCP tools
work today but require the user (or model) to phrase the right
request. Commands are markdown files with frontmatter — nearly free
to add — and surface those tools as first-class `/`-prefixed
operations in chat.

**Initial command set:**

- `/autocontext-rules <topic>` — wraps
  `search_autocontext_instructions_files_by_content` and renders the
  top three hits' headings + section excerpts.
- `/autocontext-rules-for <path-or-glob>` — wraps
  `list_autocontext_instructions_files` with `applyTo` set, then
  `get_autocontext_instructions_file` for each match.
- `/autocontext-status` — invokes a hook script that returns a
  snapshot of: MCP server health (via the existing
  [HealthMonitorServer](../../src/AutoContext.VsCode/src/health-monitor-server.ts)),
  worker registry state (from the
  [worker-manager](../../src/AutoContext.VsCode/src/worker-manager.ts)
  via `worker-control` service address), enabled vs disabled tools
  (from the `AutoContextConfigSnapshot`), and the loaded
  instructions count (from `InstructionsFilesManifest`).
- `/autocontext-check-commit` — runs `analyze_git_commit_message` on
  whatever message the user pastes after the command.

Each command is a single `commands/<name>.md` file containing:

```markdown
---
description: Search AutoContext instruction files by content.
arguments:
  - name: topic
    description: Free-text query.
    required: true
---

Use the `search_autocontext_instructions_files_by_content` tool with
`query` = `{topic}`. Render the top three matches' name, score, and
section excerpts. If nothing matches, fall back to
`list_autocontext_instructions_files` with no filters.
```

**Files touched:**

- New folder: `src/AutoContext.VsCode/plugin/commands/`.
- One markdown file per command (4 files initial set).
- No build-pipeline change — markdown files ship as-is via the VSIX
  (the plugin folder is already bundled).

**Tests:**

- Smoke test in
  `src/AutoContext.VsCode/tests/smoke-tests/plugin-commands.test.ts`
  asserting each command markdown is well-formed (frontmatter parses,
  required `description` is present).

**Risks:**

- Each command's body is a prompt to the model. Drift in tool names
  breaks them silently. Mitigation: the smoke test parses each
  markdown body and asserts referenced tool names exist in
  [package.json](../../src/AutoContext.VsCode/package.json) under
  `contributes.languageModelTools` plus the MCP registry.

---

### Phase 3 — Plugin-bundled `instructions/` (portability)

**One-line:** copy
[ALWAYS_ATTACHED_INSTRUCTIONS_FILES](../../src/AutoContext.VsCode/src/always-attached-instructions-files.ts)
into `plugin/instructions/` so the plugin works under any Claude
client, not just our VSIX.

**Why:** the SessionStart hook today resolves the instructions via
`path.resolve(__dirname, '..', '..')` to walk up to
`<extension>/instructions/`. That path layout is specific to our
VSIX. Claude Code / Desktop installing the same plugin folder
elsewhere have no `<extension>/instructions/` peer, so the hook
prints "could not read…" to stderr and emits `{}`.

**Scope:**

- Stage `instructions/copilot.instructions.md` and
  `instructions/autocontext.instructions.md` into
  `plugin/instructions/` during build (extend
  [build.ps1](../../build.ps1) `Build-TypeScript`'s post-compile
  block, alongside the existing hook-script copy).
- Update `autocontext-session-start.cts` to read from
  `${CLAUDE_PLUGIN_ROOT}/instructions/` first, falling back to
  `<extension>/instructions/` for backward compatibility during the
  transition.
- Add `plugin/instructions/*.instructions.md` to
  [.gitignore](../../src/AutoContext.VsCode/.gitignore) (generated
  artefacts, like the staged `*.cjs`).

**Files touched:**

- `build.ps1` — copy block.
- `src/AutoContext.VsCode/src/hooks/autocontext-session-start.cts` —
  resolution order.
- `src/AutoContext.VsCode/.gitignore`.
- `src/AutoContext.VsCode/.vscodeignore` — verify
  `plugin/instructions/**` is included (no rule overrides it; the
  default-include `plugin/**` already covers it; same as for
  `plugin/scripts/**`).

**Tests:**

- The existing smoke test for SessionStart still passes against the
  bundled-extension layout.
- New direct-script smoke: run the hook with `CLAUDE_PLUGIN_ROOT`
  pointed at a temp directory containing only the staged plugin
  folder, assert the output contains the meta-instructions header.

**Risks:**

- File duplication: the same body is shipped twice (under
  `<extension>/instructions/` and under `plugin/instructions/`).
  Acceptable: build copies, single source of truth in repo.
  Document explicitly in the plan and surface a build-time check
  that the two paths' content matches.

---

### Phase 4 — `agents/` sub-agents

**One-line:** ship pre-configured sub-agents with curated tool /
instruction allow-lists for common roles.

**Why:** with 79 instruction files plus the LM-tool surface, the
agent has to *find* the right rules every turn. A "Reviewer"
sub-agent pre-loads the review-relevant subset; a "Test author"
sub-agent pre-loads the testing stack; etc. The main agent spawns
the sub-agent when the conversation matches.

**Initial sub-agent set:**

- **`autocontext-reviewer`** — read-only. Pre-loads
  `code-review.instructions.md` plus language stacks. Tool allow-list:
  every `read_*` / `analyze_*` / `search_*` / `list_*`. No write
  tools.
- **`autocontext-test-author`** — pre-loads `testing.instructions.md`
  plus the matching test-framework instructions
  (`web-vitest.instructions.md`, `web-mocha.instructions.md`,
  `web-playwright.instructions.md`, `dotnet-xunit.instructions.md`,
  `dotnet-testing.instructions.md`). Write tools enabled.
- **`autocontext-dotnet-refactor`** — pre-loads
  `lang-csharp.instructions.md`,
  `dotnet-coding-standards.instructions.md`,
  `dotnet-async-await.instructions.md`,
  `dotnet-core.instructions.md`,
  `dotnet-performance-memory.instructions.md`,
  `design-principles.instructions.md`. Tool allow-list: full
  read/write set + the four C#-related MCP tools.
- **`autocontext-commit-author`** — pre-loads
  `git-commit-format.instructions.md`. Tool allow-list:
  `analyze_git_commit_message` plus read tools. No write.

Each sub-agent is a `agents/<name>.md` markdown file with frontmatter
declaring its `description`, `tools`, and `instructions` (the spec
fields per the Claude plugin docs).

**Files touched:**

- `src/AutoContext.VsCode/plugin/agents/` (new folder, 4 markdown
  files).

**Tests:**

- Frontmatter validation as for slash commands.
- Tool-name allow-lists cross-checked against the actual contributed
  LM tools and MCP registry.

**Risks:**

- Sub-agent invocation is host-dependent — VS Code Copilot may not
  honour the spec's `agents/` field today. Mitigation: ship the
  files anyway; they're inert when the host doesn't read them, and
  fully active when run under Claude Code. Worst case = no harm.
  Verify experimentally before committing to the test list.

---

### Phase 5 — `PreToolUse` "did you check the rules?" reminder

**One-line:** before the agent calls a write tool, check whether the
relevant instruction file has been read this session, and if not,
inject a one-line reminder.

**Why:** the SessionStart hook already injects the meta-instructions,
but for files in less-common languages / contexts (e.g. PowerShell,
YAML, SQL), the agent may write without consulting the matching
instruction file. PreToolUse can detect "you're about to write a
`*.ps1` and you have not invoked `get_autocontext_instructions_file`
for `lang-powershell.instructions.md` yet" and inject:

> Before writing, call `get_autocontext_instructions_file` with
> `name: lang-powershell.instructions.md`.

**Scope:**

- New hook
  `src/AutoContext.VsCode/src/hooks/autocontext-pre-tool-use.cts`.
- Maintain a session-scoped in-memory record of which instruction
  files have been read. Implementation: write a small ledger to a
  per-session temp file (path passed via the hook's `session_id`
  field per the spec; falls back to a hash if unavailable).
- Map `target file extension/glob → instruction-file name(s)` from
  the metadata's `applyTo` glob (read from
  [resources/instructions-files.metadata.json](../../src/AutoContext.VsCode/resources/instructions-files.metadata.json)
  — the same metadata the LM-tool surface uses).
- Skip the reminder if the file matched is a non-source artefact
  (lock files, binaries) or if all matching instruction files have
  already been read this session.

**Files touched:**

- `plugin/hooks/hooks.json` — add `PreToolUse`.
- `src/AutoContext.VsCode/src/hooks/autocontext-pre-tool-use.cts` —
  new.
- `build.ps1` — copy block (same pattern).
- `src/AutoContext.VsCode/src/hooks/session-ledger.cts` — shared
  helper between Pre/Post hooks.

**Risks:**

- **Premature reminder noise.** If the model already had the file
  injected at SessionStart (those two are always-attached), the
  reminder for them is redundant. Mitigation: bake
  `ALWAYS_ATTACHED_INSTRUCTIONS_FILES` into the ledger initially.
- **Wrong-direction nag.** If the model is reading a `*.ts` file
  but writing a `*.json`, we should reason about the *write target*
  not the *read context*. Mitigation: hook only fires on writes;
  matches against the write target's path.

---

### Phase 6 — `mcpServers` declared in `plugin.json` (cross-IDE)

**One-line:** add the AutoContext MCP server to the plugin manifest
so non-VS-Code Claude clients (Claude Code, Claude Desktop) get the
analyzer tools when they install the plugin folder.

**Why:** today the MCP server is registered through VS Code's
[McpServerProvider](../../src/AutoContext.VsCode/src/mcp-server-provider.ts)
— invisible to other Claude hosts. The plugin spec lets us declare
the same server in `plugin.json`'s `mcpServers` field, making the
five analyzer tools available wherever Claude consumes the plugin.

**Scope:**

- Add `mcpServers` to `plugin/.claude-plugin/plugin.json`. The
  binary name is per-platform (`AutoContext.Mcp.Server.exe` on
  Windows, `AutoContext.Mcp.Server` elsewhere — VS Code's
  `McpServerProvider` derives this with `process.platform === 'win32'
  ? '.exe' : ''`); the plugin manifest is static JSON, so we ship
  one VSIX per RID and bake the matching name into each:

  ```jsonc
  // plugin.json shipped in the win32-x64 VSIX
  {
      "name": "autocontext",
      "description": "...",
      "author": { "name": "2site.net" },
      "mcpServers": {
          "autocontext-mcp-server": {
              "command": "${CLAUDE_PLUGIN_ROOT}/../servers/AutoContext.Mcp.Server/AutoContext.Mcp.Server.exe",
              "args": []
          }
      }
  }
  ```

- The path token resolves at plugin-runtime to the same per-platform
  binary the VS Code `McpServerProvider` already spawns (different
  arg set, since the plugin-spawned server has no extension to talk
  to — runs with a plain stdio surface, no `--service` channels).
- Implementation note: the per-RID `plugin.json` is generated during
  packaging, not committed. Add the generation to the existing
  multi-platform packaging step in [build.ps1](../../build.ps1)
  (`Build-VscePackage`).

**Critical constraint:** the embedded VS Code path keeps using
`McpServerProvider` so the existing `--service log=…`,
`--service health-monitor=…`, `--service worker-control=…`,
`--service extension-config=…` channels remain intact. The plugin
manifest only registers the server for **non-VS-Code hosts**; in VS
Code, Copilot's MCP picker may show a duplicate. Coordinate with the
plan in
[mcp-tool-registration-suppression.md](mcp-tool-registration-suppression.md):
when the suppression flag is implemented, the embedded server (and
only that one) suppresses tool registration; the plugin-launched
server remains the canonical surface for non-VS-Code clients.

**Files touched:**

- `plugin/.claude-plugin/plugin.json` — add `mcpServers`.

**Tests:**

- Manual: install the plugin folder under Claude Desktop; confirm
  the five analyzer tools appear.
- Automated: a smoke test that boots
  `AutoContext.Mcp.Server.exe` directly (no `--service` args) and
  confirms it advertises the five tools without crashing.

**Risks:**

- **Path resolution.** `${CLAUDE_PLUGIN_ROOT}` resolves to the
  plugin folder, but the .NET binary is staged under
  `<extension>/servers/AutoContext.Mcp.Server/`, which is *outside*
  the plugin folder. The relative path `../../servers/...` works
  inside our VSIX but is brittle if Claude Desktop installs the
  plugin somewhere not adjacent to a server folder. Mitigation:
  ship the server binary **inside** `plugin/` for non-VSIX consumers
  (would balloon the plugin folder ~50 MB). Decision deferred to
  Phase 6 implementation; may pick "VSIX-only for now".
- **Tool duplication inside VS Code.** Until the suppression flag
  lands, both the embedded and the plugin-spawned server expose the
  same tools, doubling the MCP picker entries. Mitigation: don't
  ship Phase 6 until
  [mcp-tool-registration-suppression.md](mcp-tool-registration-suppression.md)
  is implemented. Phase 6 is **gated on that plan**.

---

## Cross-cutting Concerns

### Hook resilience

Every hook follows the safety stance of the existing SessionStart
script:

- **Read-only.** No filesystem mutation outside per-session ledger
  writes (Phase 5).
- **Time-bounded.** All hooks declare `timeout: 10` in
  [hooks.json](../../src/AutoContext.VsCode/plugin/hooks/hooks.json).
- **Failure-tolerant.** On any error, emit `{}` (no-op) and exit 0;
  the agent transcript captures stderr, but the chat session never
  blocks.
- **Stateless across agents.** Hooks read state only from the
  `${CLAUDE_PLUGIN_ROOT}` directory and per-session inputs; no
  global cache.

### Build-pipeline reuse

The `.cts → .cjs` pipeline already established by Phase 0 (existing
SessionStart) generalises:

1. New `.cts` source under `src/hooks/`.
2. `tsc` emits `.cjs` into `dist/hooks/` (Node16 module mode).
3. `build.ps1`'s `Build-TypeScript` post-step copies into
   `plugin/scripts/`.
4. `.vscodeignore` keeps `dist/hooks/**` out of the VSIX while
   `!dist/**` allows the rest of `dist/` to ship as today; the
   plugin folder ships normally.

Phases 1 and 5 add new `.cts` files. Phase 3 adds a markdown-copy
step. Phases 2 and 4 add only static markdown — no compile.

### Versioning

No version bump implied by any phase. New plugin assets ship in the
next ordinary release. **Per repo policy: do not bump versions
without explicit user permission via `versionize.ps1`.**

### Cross-IDE coordination

Phases that change the plugin manifest (`plugin.json` /
`hooks.json`):

- **Phase 1** — adds `PostToolUse`. VS Code Copilot's hook support
  level needs verification before rolling out broadly.
- **Phase 6** — adds `mcpServers`. Gated on suppression plan.

Phases that ship inert files in non-supporting hosts (no harm done):

- **Phase 2** — `commands/` files.
- **Phase 4** — `agents/` files.

Phase 3 changes hook script behaviour but preserves the
`<extension>/instructions/` fallback; safe for existing hosts.

## Risks (Plan-Level)

- **Spec drift.** The Claude plugin spec is young. Field names
  (`hooks` schema, frontmatter shape) may evolve. Mitigation: per
  phase, reference the Anthropic plugin docs at the time of
  implementation; pin a known-good schema version in repo notes.
- **Host disagreement.** VS Code Copilot, Claude Code, and Claude
  Desktop may interpret the plugin manifest differently. Phases 2
  and 4 are explicitly inert in non-supporting hosts; Phases 1 and 5
  require host hook support; Phase 6 requires host MCP-from-plugin
  support.
- **Maintenance load.** Each new hook is one more script tracked in
  the .cts → .cjs pipeline. Mitigation: phases land independently;
  if a phase doesn't pull its weight, it can be removed without
  affecting others.

## Files Likely Touched (across all phases)

```
src/AutoContext.VsCode/plugin/.claude-plugin/plugin.json   (Phase 6)
src/AutoContext.VsCode/plugin/hooks/hooks.json             (Phases 1, 5)
src/AutoContext.VsCode/plugin/commands/<name>.md           (Phase 2 — new)
src/AutoContext.VsCode/plugin/agents/<name>.md             (Phase 4 — new)
src/AutoContext.VsCode/plugin/instructions/                (Phase 3 — staged)
src/AutoContext.VsCode/plugin/scripts/                     (generated, all phases)
src/AutoContext.VsCode/src/hooks/autocontext-post-tool-use.cts  (Phase 1 — new)
src/AutoContext.VsCode/src/hooks/autocontext-pre-tool-use.cts   (Phase 5 — new)
src/AutoContext.VsCode/src/hooks/session-ledger.cts             (Phase 5 — new)
build.ps1                                                  (Phases 1, 3, 5)
src/AutoContext.VsCode/.gitignore                          (Phase 3)
src/AutoContext.VsCode/.vscodeignore                       (Phase 3 — verify)
src/AutoContext.VsCode/tests/unit-tests/hooks/             (Phases 1, 5)
src/AutoContext.VsCode/tests/smoke-tests/plugin-commands.test.ts  (Phase 2)
```

## Cross-References

- [docs/architecture.md → Instruction Discovery (LM Tools)](../architecture.md#instruction-discovery-lm-tools)
  — describes the four extension-native LM tools that Phase 2's
  slash commands wrap.
- [docs/future/mcp-tool-registration-suppression.md](mcp-tool-registration-suppression.md)
  — Phase 6 gate. Describes the `--suppress-tools-registration` flag
  that would let the embedded VS Code MCP server share a process with
  a plugin-declared MCP server without duplicating tool registration.
- [docs/future/autoctx-cli.md](autoctx-cli.md) — non-VS-Code host
  context for Phase 6.
- [src/AutoContext.VsCode/src/always-attached-instructions-files.ts](../../src/AutoContext.VsCode/src/always-attached-instructions-files.ts)
  — Phase 3 source list.
- [src/AutoContext.Mcp.Server/mcp-workers-registry.json](../../src/AutoContext.Mcp.Server/mcp-workers-registry.json)
  — Phase 1 and Phase 2 reference this for analyzer name + parameter
  shape.

## Status

- **Phase 0 (shipped):** SessionStart hook, plugin scaffolding,
  installer.
- **Phases 1–6:** not started. Each ships independently when it
  reaches the top of the queue.
