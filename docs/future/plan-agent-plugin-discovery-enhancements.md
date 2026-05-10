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
| Plugin-bundled instructions | sourced from `<extension>/instructions/` via `__dirname` walk | Could be served on-demand by a bundled CLI daemon for cross-host portability (this plan) |

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

## Design Principles

These principles bind every step in this plan. They are explicit
because earlier drafts violated them.

- **Reuse over reimplementation.** Hooks, commands, and sub-agents
  call into existing extension components —
  [InstructionsFilesManifest](../../src/AutoContext.VsCode/src/instructions-files-manifest.ts),
  [InstructionsFilesLmToolsApplyToMatcher](../../src/AutoContext.VsCode/src/instructions-files-lm-tools-apply-to-matcher.ts),
  [McpToolsManifest](../../src/AutoContext.VsCode/src/mcp-tools-manifest.ts),
  [HealthMonitorServer](../../src/AutoContext.VsCode/src/health-monitor-server.ts),
  [WorkerManager](../../src/AutoContext.VsCode/src/worker-manager.ts) —
  rather than reimplementing path matching, manifest loading, or
  worker dispatch in hook scripts.
- **Single source of truth.** Each artefact lives in exactly one
  place. The curated instruction corpus lives under
  `<extensionPath>/instructions/` only and is read at runtime by
  the `autoctx` daemon's `InstructionsCorpusReader`; nothing is
  copied into the plugin folder. The MCP tool routing table lives
  in `mcp-workers-registry.json` only (hooks consume it via the
  daemon, they do not duplicate it). Per-RID variants of
  `plugin.json` are generated, not committed.
- **Class-based, no free functions.** Every new module is a class,
  matching the codebase pattern (`AgentPluginInstaller`,
  `InstructionsFilesManager`, `McpToolsManifestLoader`,
  `WorkerManager`, ...). The existing free-function
  [autocontext-session-start.cts](../../src/AutoContext.VsCode/src/hooks/autocontext-session-start.cts)
  is refactored to a class as part of Phase 0a.
- **Breaking changes are acceptable.** The extension is in preview;
  steps remove fallbacks and consolidate paths rather than carrying
  legacy code. No `<extension>/instructions/` compatibility shim.
- **Per-step tests, not per-phase tests.** Each step lands with its
  own unit / smoke tests in the same commit. A phase that adds three
  steps adds three test files, not one batched test file.
- **Docs land with the step.** README, `docs/architecture.md`, and
  walkthroughs that describe the new surface are updated in the same
  step that ships it.

## Shared Infrastructure

Introduced once in Phase 0a, reused by every later phase. All names
are classes per the design principles. The single most important
dependency is the **`autoctx` CLI daemon** specified in
[autoctx-cli.md](./autoctx-cli.md): hooks, slash commands, and
sub-agents all consume instruction state and configuration through
the daemon's named-pipe RPC surface (`Instructions.GetAll`,
`Instructions.List`, `Config.Get`, etc.) rather than reading files
from disk. There is no projection into `plugin/instructions/` and no
lock-file dance — the daemon owns the in-memory truth and is the
only writer.

- **`HookRunner`** — abstract base class for every hook script
  (`src/AutoContext.VsCode/src/hooks/hook-runner.cts`). Owns the
  stdin drain, JSON envelope emission, error swallowing, and
  `{}`-on-failure contract. Subclasses implement
  `protected abstract runCore(input: HookInput): Promise<HookOutput | null>`.
- **`PluginRootResolver`** — resolves
  `${CLAUDE_PLUGIN_ROOT}` (preferred), then `__dirname` walk
  fallback. Replaces today's ad-hoc
  `path.resolve(__dirname, '..', '..')` in the SessionStart script.
- **`WorkspaceContextResolver`** — resolves the active workspace
  root for hook scripts. Priority order:
  `${CLAUDE_PROJECT_DIR}` (Claude Code), then the hook-input JSON's
  `cwd` field (PostToolUse / PreToolUse payloads carry it), then
  `process.cwd()`. Returns the resolved absolute path; the workspace
  hash is owned by the daemon (it derives the pipe name) and is not
  recomputed in hooks.
- **`AutoctxBinaryResolver`** — resolves the absolute path of the
  bundled `autoctx` binary by joining the resolved plugin root with
  `cli/<rid>/autoctx[.exe]` (per the per-RID layout defined in the
  CLI plan's *Distribution* section). Throws when missing so hooks
  fail fast rather than emitting `{}` for what should be a packaging
  bug.
- **`AutoctxClient`** — thin wrapper over the daemon pipe: connect,
  optionally spawn `autoctx daemon --workspace <path>` on cold
  start, perform a single RPC, disconnect. Used by every hook that
  needs instruction bodies, config snapshots, or the instructions
  list. Lives in `Framework.Web/src/cli/` so it can be shared with
  any future host.
- **Shared CJS exports.** `HookRunner`, `PluginRootResolver`,
  `WorkspaceContextResolver`, `AutoctxBinaryResolver`, and
  `AutoctxClient` are emitted as CJS so the hook scripts (compiled
  `.cjs` under `plugin/scripts/`) can `require()` them. The
  extension and `Framework.Web` consume the same modules through
  default-CJS interop.
## Existing-Feature Interactions

The extension already implements rich state around
enable/disable/override that every new surface must respect. The
plan-as-written previously ignored several of these. They are
called out here once and referenced by the affected phases.

### State sources

| Concern | Owner class | Persistence | What it controls |
|---------|------------|-------------|------------------|
| Disable an entire instruction file | [AutoContextConfigManager.setInstructionEnabled](../../src/AutoContext.VsCode/src/autocontext-config-manager.ts) | `.autocontext.json` `instructions[name].enabled` | The file is excluded from `chatInstructions`, the LM list/get tools, and (under this plan) the daemon's `Instructions.GetAll` response — so hooks never see it. |
| Disable an individual rule inside a file | [AutoContextConfigManager.toggleInstruction](../../src/AutoContext.VsCode/src/autocontext-config-manager.ts) | `instructions[name].disabledInstructions: string[]` | A subset of bullets/sections is filtered out by [InstructionsFileContentProjector](../../src/AutoContext.VsCode/src/instructions-file-content-projector.ts) (today, in-extension) or by the daemon's `InstructionsCorpusService` (under this plan). Hooks always read projected bodies from the daemon, never the raw file. |
| Disable an entire MCP tool | [AutoContextConfig.isToolEnabled](../../src/AutoContext.VsCode/src/autocontext-config.ts) | `mcpTools[name] === false` or `{ enabled: false }` | The tool isn't advertised by the MCP server (suppressed via `--service extension-config=…` snapshot). |
| Disable an MCP task under a tool | `AutoContextConfig.isToolEnabled(tool, task)` | `mcpTools[name].disabledTasks: string[]` | The task isn't advertised; the tool stays. |
| Instruction override (workspace-local) | [InstructionsFilesOverrideWatcher](../../src/AutoContext.VsCode/src/instructions-files-override-watcher.ts) | `.github/instructions/*.instructions.md` in the workspace | Sets `setContext('autocontext.override.<name>', true)`; the `chatInstructions` `when` clause swaps to the local file. |
| Export curated file into a workspace override | [InstructionsFilesExporter](../../src/AutoContext.VsCode/src/instructions-files-exporter.ts) + tree-view export mode | Writes `.github/instructions/<name>` | Existing UI surface; **don't** add a `/autocontext-export` slash command that duplicates it. |

### Required hook / command behaviour

- **All state goes through the daemon.** SessionStart, PreToolUse,
  and PostToolUse hooks talk to the per-workspace `autoctx` daemon
  via `AutoctxClient`. They never read `.autocontext.json`,
  `<extensionPath>/instructions/<name>`, or `plugin/instructions/<name>`
  directly. The daemon enforces every disable rule centrally and
  hands back already-filtered, already-projected results. A hook
  cannot accidentally see disabled state because the daemon never
  sends it.
  - For instruction bodies: `Instructions.GetAll` / `Instructions.Get(name)`
    — returns projected markdown with disabled rules removed and
    `[INSTxxxx]` tags stripped. Files where `enabled === false` are
    omitted from `GetAll` and return `null` from `Get`.
  - For listings (e.g. PreToolUse "did the agent read this?"
    matchers): `Instructions.List` — returns one entry per file
    with name, enabled flag, override-present flag, and `applyTo`
    glob. The reminder lookup uses these flags directly; no
    filesystem probing.
  - For tool/task disable: `Config.Get` returns the
    [AutoContextConfig](../../src/AutoContext.VsCode/src/autocontext-config.ts)
    snapshot; hooks call `isToolEnabled(tool, task?)` on the
    snapshot before emitting any prompt that would reference a
    disabled tool or task.
- **Single source of truth: `.autocontext.json` (read by the daemon
  only).** All disable state lives in `.autocontext.json`. The
  daemon watches it via `AutoContextConfigStore` and pushes change
  events to subscribed clients. There is no on-disk projection
  artifact, no `<name>.disabled` filename suffix, no sidecar JSON.
  Re-running `Instructions.GetAll` after a config change returns the
  updated bodies; the previous values are discarded.
- **Override resolution is daemon-side.** When a workspace override
  exists at `.github/instructions/<name>`, the daemon's
  `InstructionsCorpusReader` (per the CLI plan) feeds the override
  body into the projector instead of the bundled raw source. Hooks
  receive the projected override body transparently from
  `Instructions.Get(name)`. `Instructions.List` reports
  `overridden: true` so a UI surface (e.g. `/autocontext-status`)
  can mention it without an extra probe.
- **Sub-agent `instructions:` paths point at a per-session
  materialisation cache.** Claude sub-agents require static file
  paths in their frontmatter; the SessionStart hook materialises
  the projected bodies into the OS-specific cache directory
  documented in the CLI plan (`%LOCALAPPDATA%\autocontext\<hash>\`
  on Windows; `$XDG_CACHE_HOME/autocontext/<hash>/` or
  `~/.cache/autocontext/<hash>/` on POSIX) and the sub-agent
  manifests reference that location. The cache is not the source
  of truth — it is rewritten on every SessionStart from the
  daemon's projected output.
- **Tasks, not just tools.** The PostToolUse router resolves the
  target *task* (e.g. `analyze_typescript_code.<task>`) when the
  registry decomposes the tool into tasks, and consults
  `isToolEnabled(tool, task)` against the daemon-supplied config
  snapshot before emitting an analyzer prompt.

### Daemon ownership

Projection of curated instructions runs **only** inside the
`autoctx` daemon. There is no second projector and no lock file. The
daemon's `InstructionsCorpusService` (see
[autoctx-cli.md](./autoctx-cli.md) Phase 4 step 3) owns the file
watchers, the projection algorithm, and the change-event stream.
Every host — VS Code extension, Claude SessionStart hook, Claude
sub-agent dispatcher, future JetBrains/Neovim shells — reads
projected bodies from the daemon over IPC.

The extension is a daemon **client**, not a co-projector. On
activation it connects to the workspace's daemon (spawning it on
cold start), subscribes to `Instructions.Subscribe`, and on each
event rewrites the materialisation cache that `chatInstructions`
points at (per the CLI plan's *Projection ownership* section).
Deactivation drops the connection; the daemon's idle-timeout shuts
it down when the last client leaves.

Known caveats, documented:

- **Mid-session staleness on Claude hosts.** SessionStart fires
  once. The materialisation cache it writes reflects the daemon's
  state at that moment. If `.autocontext.json` changes mid-session,
  the next sub-agent dispatch sees the stale cache. Mitigation:
  Phase 5 (PreToolUse) re-materialises opportunistically on
  `Instructions.Subscribe` events received during the session.
- **Cold-start latency on Claude.** First SessionStart in a fresh
  workspace spawns the daemon; subsequent calls are warm. Cold
  start cost is the `autoctx daemon` process boot — measured at
  packaging time, documented as a one-time per-workspace cost.
- **Read-only `${CLAUDE_PLUGIN_ROOT}`.** The plugin folder ships
  static assets (hooks, the bundled `autoctx` binary, `plugin.json`).
  Nothing is written there at runtime, so read-only mounts are
  fully supported — a fundamental improvement over the in-plugin
  projection model. The materialisation cache lives in the OS cache
  directory, which is always user-writable.

## Phases

The phases are independent and each ships its own VSIX. Order is by
ROI; nothing later depends on something earlier landing first
unless flagged explicitly. **Phase 0a is the prerequisite for every
other phase** — each one uses `HookRunner` (Phase 0a.1) and/or
`AutoctxClient` (Phase 0a.2). Phase 0a is itself gated on the
`autoctx` CLI's daemon slice being shippable. Phase 0a lands first.

---

### Phase 0a — Prep: hooks become daemon clients

**One-line:** unblock every later phase by extracting the shared
hook infrastructure (`HookRunner`, resolvers, `AutoctxClient`) and
rebuilding the SessionStart hook as a thin client of the `autoctx`
daemon defined in [autoctx-cli.md](./autoctx-cli.md).

**Why:** every later phase (PostToolUse routing, slash commands,
sub-agents with `instructions:` frontmatter, PreToolUse reminders)
needs a single, host-independent way to obtain projected
instruction bodies, the disable-aware tool list, and the workspace
config snapshot. The daemon owns all of that; this phase is what
teaches the hooks to talk to it.

**Hard prerequisite:** the `autoctx` CLI's *Phase 4 — Daemon +
`autoctx instructions`* slice must be shippable. This plan does not
duplicate that work; see
[autoctx-cli.md](./autoctx-cli.md#phase-4--daemon--autoctx-instructions)
for the daemon, projector, and corpus-reader implementation.
Progress on Phase 0a is gated on the CLI's *second validation
slice* being green end-to-end against a real Claude Code session.

**Steps:**

- **Step 0a.1 — Extract `HookRunner`, `PluginRootResolver`,
  `WorkspaceContextResolver`, `AutoctxBinaryResolver`.** New `.cts`
  modules under `src/AutoContext.VsCode/src/hooks/`. Tests:
  `tests/unit-tests/hooks/hook-runner.test.ts`,
  `tests/unit-tests/hooks/plugin-root-resolver.test.ts`,
  `tests/unit-tests/hooks/workspace-context-resolver.test.ts`,
  `tests/unit-tests/hooks/autoctx-binary-resolver.test.ts` — the
  binary resolver test covers per-RID layout, missing-binary
  failure mode, and `${CLAUDE_PLUGIN_ROOT}` precedence.
- **Step 0a.2 — Build `AutoctxClient` in `Framework.Web`.** Thin
  pipe-RPC wrapper with `connect()`, `instructions.list()`,
  `instructions.get(name)`, `instructions.getAll()`,
  `config.get()`, and `subscribe(channel, listener)`. Spawns
  `autoctx daemon --workspace <path>` on cold connect. Reuses the
  pipe-name derivation from the daemon. Tests:
  `tests/unit-tests/cli/autoctx-client.test.ts` (in-process
  daemon stub) and a smoke test that launches the real daemon and
  round-trips `Instructions.List`.
- **Step 0a.3 — SessionStart as daemon client.**
  `autocontext-session-start.cts` becomes a thin entry point that
  instantiates `SessionStartHookRunner extends HookRunner`. The
  runner:
  1. Resolves the workspace via `WorkspaceContextResolver`.
  2. Resolves the bundled `autoctx` binary via
     `AutoctxBinaryResolver`.
  3. Connects (or spawns) the workspace's daemon via
     `AutoctxClient`.
  4. Calls `Instructions.GetAll` to obtain the projected bodies
     for every enabled curated file (override-aware,
     disable-aware — enforced server-side).
  5. Materialises those bodies into the OS-specific cache
     directory (`%LOCALAPPDATA%\autocontext\<hash>\` on Windows;
     `$XDG_CACHE_HOME/autocontext/<hash>/` or
     `~/.cache/autocontext/<hash>/` on POSIX) so any sub-agents
     dispatched later in the session can reference them by
     absolute path. The cache is rewritten in full on every
     SessionStart — idempotent, no merge logic.
  6. Emits the always-attached pair as `additionalContext`
     (today's behaviour, preserved). The bodies come from the
     same `Instructions.GetAll` response so disabled rules in
     those files are filtered identically.
  All daemon errors are swallowed; on failure the runner emits
  `{}` per the plugin spec. Tests:
  `tests/unit-tests/hooks/session-start-hook-runner.test.ts` —
  cases include: daemon-cold-start, daemon-already-running,
  daemon-unreachable → `{}`, file-disabled → absent from cache,
  rule-disabled → projected body in cache, override-present →
  override projected, repeat-run → idempotent cache rewrite.
- **Step 0a.4 — Bundle `autoctx` into the VSIX.** Wire
  `build.ps1 Package` to copy the per-RID self-contained `autoctx`
  binary into `src/AutoContext.VsCode/plugin/cli/<rid>/` ahead of
  VSIX assembly. The CLI plan's *Distribution* section pins the
  layout. `.vscodeignore` allows `plugin/cli/**` through. Test:
  `tests/smoke-tests/plugin-cli-bundling.test.ts` asserts the
  staged plugin folder contains a runnable `autoctx[.exe]` for
  the host RID.
- **Step 0a.5 — Retire on-disk projection in the extension.**
  Delete `<extensionPath>/instructions/.generated/`,
  `InstructionsFilesManager`'s projection writes, and
  `package.json` `chatInstructions` paths into `.generated/`. The
  extension becomes a daemon client (per the CLI plan's
  *Projection ownership* section): on activation it connects to
  the daemon, calls `Instructions.GetAll`, and writes the
  materialisation cache under
  `<extensionPath>/instructions.cache/<workspace-hash>/` that the
  re-pointed `chatInstructions` paths resolve relative to. The
  cache is rewritten on every `Instructions.Subscribe` event.
  Tests: `instructions-files-manager.test.ts` is rewritten around
  the new cache-materialiser role; the projection unit tests move
  to the daemon's `InstructionsCorpusService` (per the CLI plan).
- **Step 0a.6 — Build & docs.** `.vscodeignore` excludes
  `instructions.cache/**` from the VSIX (it's runtime state).
  Update `docs/architecture.md` with a new *Bundled Agent-Plugin*
  section describing the daemon-client model + the materialisation
  cache. Update `src/AutoContext.VsCode/README.md` with the
  bundled-CLI note.

**What does NOT change:**

- `.autocontext.json` schema and semantics are unchanged. Disable
  state still lives there and only there.
- `InstructionsFilesOverrideWatcher` and `.github/instructions/<name>`
  override semantics are unchanged from the user's perspective —
  the daemon's `InstructionsCorpusReader` handles the precedence.
- LM tools (`list_*`, `search_*`, `get_*`) read projected bodies;
  inside the extension they continue to do so via the cache that
  Step 0a.5 maintains.
- Always-attached injection in SessionStart is unchanged in
  effect; only the body source changes (daemon → hook → envelope
  instead of bundled-file → hook → envelope).

**Risks:**

- **Daemon unreachable.** If the bundled `autoctx` binary fails to
  spawn (corrupt VSIX, antivirus quarantine), every hook degrades
  to `{}`. Mitigation: `AutoctxBinaryResolver` throws clearly on
  missing binary, hook logs to stderr, packaging validator
  (Phase 3) asserts the binary's presence.
- **Cold-start latency on first SessionStart.** Spawning the
  daemon adds startup cost to the first hook of the first session
  in a workspace. Subsequent sessions reuse the warm daemon.
  Measure during smoke-testing; if unacceptable, add a
  pre-warming step to extension activation so VS Code workflows
  inherit a warm daemon.
- **Materialisation-cache permissions.** The OS cache dir is
  user-writable on every supported platform; failure is unusual
  but should be logged. Sub-agents cannot dispatch without it.
- **Sub-agent loader strictness.** Claude hosts may resolve
  sub-agent `instructions:` paths at install time vs. dispatch
  time. The materialisation cache exists by SessionStart and is
  fresh on every session, so dispatch-time resolution is safe;
  install-time resolution would fail. Smoke-test all three
  target hosts (VS Code Copilot, Claude Code, Claude Desktop)
  before Phase 4 ships sub-agents. Fallback: bake a
  build-time-baseline copy into `plugin/instructions/` for
  install-time resolvers (would be unprojected; documented as a
  degraded mode for that host).

---

### Phase 1 — `PostToolUse` analyzer hook (highest ROI)

**One-line:** after the agent writes a file, the matching analyzer
runs automatically and the findings come back as `additionalContext`
for the next turn.

**Why:** today the MCP analyzers fire only when the agent chooses to
invoke them. A hook makes validation **inevitable**, not optional.

**Reuse:**

- Routing comes from
  [mcp-workers-registry.json](../../src/AutoContext.Mcp.Server/mcp-workers-registry.json).
  The hook does **not** carry a duplicate routing table; it reads the
  registry through the existing
  [McpToolsManifest](../../src/AutoContext.VsCode/src/mcp-tools-manifest.ts)
  class (compiled to CJS via the shared-CJS seam from Phase 0a).
- The hook's runner extends `HookRunner` (Phase 0a).
- Tool / task disable state comes from the existing
  [AutoContextConfig.isToolEnabled](../../src/AutoContext.VsCode/src/autocontext-config.ts).
  See *Existing-Feature Interactions → Required hook / command
  behaviour*.

**Steps:**

- **Step 1.1 — `PostToolUseRouter`.** New class
  `src/AutoContext.VsCode/src/hooks/post-tool-use-router.cts`. Given
  a tool name + input payload, returns
  `{ analyzer, task?, params } | null` using `McpToolsManifest` plus
  a small write-tool-name allow-list (`Write`, `Edit`, `MultiEdit`,
  `replace_string_in_file`, `create_file`,
  `multi_replace_string_in_file`, `edit_notebook_file`). Skips the
  routing decision when `AutoContextConfig.isToolEnabled(analyzer,
  task)` is false. Test:
  `tests/unit-tests/hooks/post-tool-use-router.test.ts` — covers
  enabled, tool-disabled, and task-disabled cases.
- **Step 1.2 — `PostToolUseHookRunner`.** Subclass of `HookRunner`.
  Composes the router + a `PluginRootResolver`; emits the
  `additionalContext` envelope. Test:
  `tests/unit-tests/hooks/post-tool-use-hook-runner.test.ts`.
- **Step 1.3 — Wire into manifest.** Add `PostToolUse` to
  `plugin/hooks/hooks.json` and the build's hook-staging block.
  Test: `tests/smoke-tests/post-tool-use-hook.test.ts` (boots the
  compiled `.cjs` via `node`, feeds canned PostToolUse payloads,
  asserts the JSON envelope).

**Hook output shape (PostToolUse spec):**

```jsonc
{
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": "<!-- AutoContext PostToolUse: src/Foo/Bar.cs -->\n\nYou just wrote `src/Foo/Bar.cs`. Before declaring this turn done, call `analyze_csharp_code` with `content` = the new file body and `originalPath` = the file's absolute path. If findings are returned, fix them and re-validate."
    }
}
```

The hook **does not** call MCP directly — the MCP server lives
behind the extension host's stdio pipes. The model invokes the
advertised MCP tool itself; the hook only emits the prompt.

**Docs:**

- `docs/architecture.md` — extend the "Bundled Agent-Plugin" section
  with a PostToolUse subsection (request → router → envelope).
- `walkthroughs/tools.md` — mention that analyzer chaining now
  happens automatically after writes.

**Risks:**

- Tool-name matchers drift as the host renames tools. Mitigation:
  the allow-list is a class field on `PostToolUseRouter`, covered by
  the unit test; stderr-logged when an unknown write-class tool is
  seen.
- Spammy `additionalContext` if the agent edits many files in one
  turn. Mitigation: `PostToolUseRouter` deduplicates by absolute
  path within a single invocation.

---

### Phase 2 — `commands/` slash-command surface

**One-line:** ship `/autocontext-*` slash commands that wrap the
existing LM / MCP tools so the user gets a one-shot lookup without
phrasing the right prompt.

**Why:** today's LM-instruction and analyzer tools require the user
(or model) to phrase the right request. Commands are markdown files
with frontmatter — nearly free to add — and surface those tools as
first-class `/`-prefixed operations in chat.

**Reuse:**

- `PluginCommandValidator` (new class, Step 2.1) cross-references
  the markdown bodies against the existing contributed
  `languageModelTools` array in
  [package.json](../../src/AutoContext.VsCode/package.json) and the
  MCP registry, so command drift fails CI rather than failing
  silently in chat.
- Command bodies reference existing tool names only — no parallel
  implementations.
- **Export is already a feature.** The
  [InstructionsFilesExporter](../../src/AutoContext.VsCode/src/instructions-files-exporter.ts)
  + tree-view export mode is the canonical path to write a curated
  file into `.github/instructions/` (creating an override).
  Phase 2 does **not** add a slash-command equivalent — doing so
  would compete with the existing tree-view UI and the override
  watcher.
- **Disabled state is already exposed.** The LM `list_*` and
  `search_*` tools already report `enabled` / `disabled` per file,
  and the `/autocontext-status` command surfaces those flags through
  those existing tools — no parallel state read.

**Steps:**

- **Step 2.1 — `PluginCommandValidator`.** New class +
  `tests/unit-tests/plugin/plugin-command-validator.test.ts`.
- **Step 2.2 — `/autocontext-rules`.** Markdown file under
  `plugin/commands/`. Wraps
  `search_autocontext_instructions_files_by_content`. Test:
  `tests/unit-tests/plugin/commands/autocontext-rules.test.ts`
  (validator instance + frontmatter assertions).
- **Step 2.3 — `/autocontext-rules-for`.** Wraps
  `list_autocontext_instructions_files` +
  `get_autocontext_instructions_file`. Per-step test.
- **Step 2.4 — `/autocontext-status`.** Renders a snapshot built
  entirely from existing model-callable tools: total / enabled
  instruction-file counts via `list_autocontext_instructions_files`,
  and a known-MCP-tools list pulled via
  `search_autocontext_instructions_files_by_metadata`. Internal
  extension state (health-monitor, worker registry) is **not**
  exposed here — no LM tool advertises it today, and adding one is
  out of scope per the non-goals. Per-step test.
- **Step 2.5 — `/autocontext-check-commit`.** Wraps
  `analyze_git_commit_message`. Per-step test.

Each command is a single `commands/<name>.md` file. Example shape
(`/autocontext-rules`):

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

**Docs:**

- `walkthroughs/welcome.md` — add a slash-command teaser.
- `docs/architecture.md` — list the contributed commands under the
  Bundled Agent-Plugin section.

**Risks:**

- Command bodies are prompts; tool-name drift breaks them silently.
  Mitigated by `PluginCommandValidator` running in unit tests.

---

### Phase 3 — Plugin-asset packaging guarantees

**Status:** the corpus-relocation work is gone (the daemon now owns
projection in-memory; nothing under `plugin/instructions/` ships).
This phase enforces that the *remaining* plugin assets — the hooks,
the bundled `autoctx` binary, and `plugin.json` — are present and
well-formed in every VSIX.

**Why:** the hooks are useless without the bundled `autoctx`
binary (Phase 0a.4); the binary is useless without the hook
scripts that drive it. A packaging regression in either direction
would silently produce a VSIX whose hooks return `{}` for every
session.

**Steps:**

- **Step 3.1 — `PluginAssetsValidator`.** New class invoked from
  `build.ps1 Prepare`. Given the staged plugin folder, asserts:
  - `plugin/.claude-plugin/plugin.json` exists and parses.
  - `plugin/hooks/hooks.json` exists and parses.
  - Every `command` referenced in `hooks.json` resolves under
    `plugin/scripts/` (compiled `.cjs`).
  - `plugin/cli/<host-rid>/autoctx[.exe]` exists and is
    executable.
  - `plugin/instructions/` does **not** exist in the staged
    plugin folder — the daemon owns projection at runtime; the
    VSIX must not ship a stale baseline that could be mistaken
    for current state.

  Test: `tests/unit-tests/plugin/plugin-assets-validator.test.ts`.
- **Step 3.2 — Hook into build.** Wire the validator into
  `Build-VscePackage` (or `Prepare`) so packaging fails fast on
  drift. Test: `tests/smoke-tests/plugin-assets-validation.test.ts`
  drives the validator against a synthetic broken layout and asserts
  the build step exits non-zero.

**Docs:**

- `src/AutoContext.VsCode/README.md` — packaging-guarantees note.

**Risks:**

- The validator must run after asset copy, before VSIX assembly.
  Misordering would silently no-op.

---

### Phase 4 — `agents/` sub-agents

**One-line:** ship pre-configured sub-agents with curated tool /
instruction allow-lists for common roles.

**Why:** with the curated instruction corpus plus the LM-tool
surface, the agent has to *find* the right rules every turn. A
sub-agent pre-loads the relevant subset.

**Reuse:**

- The `instructions:` frontmatter field of each sub-agent points at
  files in the per-session materialisation cache that SessionStart
  writes (Phase 0a.3) — not at `plugin/instructions/`, which no
  longer exists. The cache path is the OS-specific directory
  documented in *Required hook / command behaviour*. The daemon is
  the source of truth; the cache is a host-local materialisation
  for hosts that need static paths.
- `SubAgentDefinitionValidator` (Step 4.1) shares the frontmatter
  parser with `PluginCommandValidator` (Phase 2): one parsing class,
  two validator classes that consume it.

**Steps:**

- **Step 4.1 — `SubAgentDefinitionValidator`.** Validates frontmatter
  shape, that every `tools` entry exists in the LM-tool list or MCP
  registry, and that every `instructions` entry resolves to a name
  that the daemon would materialise (i.e. is present in
  `Instructions.List`). The validator runs at build time against a
  daemon-stub list; it does not require a running daemon. Test:
  `tests/unit-tests/plugin/sub-agent-definition-validator.test.ts`.
- **Step 4.2 — `autocontext-reviewer`.** Read-only sub-agent;
  pre-loads `code-review.instructions.md` plus language stacks.
  Tool allow-list: every `read_*` / `analyze_*` / `search_*` /
  `list_*`. No write tools. Per-step test.
- **Step 4.3 — `autocontext-test-author`.** Pre-loads
  `testing.instructions.md` plus the matching test-framework
  instructions. Write tools enabled. Per-step test.
- **Step 4.4 — `autocontext-dotnet-refactor`.** Pre-loads the C# /
  .NET stack. Full read/write set + the four C#-related MCP tools.
  Per-step test.
- **Step 4.5 — `autocontext-commit-author`.** Pre-loads
  `git-commit-format.instructions.md`. Tool allow-list:
  `analyze_git_commit_message` plus read tools. No write. Per-step
  test.

Each sub-agent is a single `agents/<name>.md` markdown file with
frontmatter declaring its `description`, `tools`, and `instructions`
(the spec fields per the Claude plugin docs).

**Docs:**

- `walkthroughs/instructions.md` — mention the sub-agent presets.
- `docs/architecture.md` — list the agents under the Bundled
  Agent-Plugin section.

**Risks:**

- Sub-agent invocation is host-dependent — VS Code Copilot may not
  honour the spec's `agents/` field today. Files are inert in
  non-supporting hosts; active under Claude Code. Verify
  experimentally before promising end-user behaviour in docs.

---

### Phase 5 — `PreToolUse` "did you check the rules?" reminder

**One-line:** before the agent calls a write tool, check whether the
relevant instruction file has been read this session, and if not,
inject a one-line reminder.

**Why:** the SessionStart hook injects the always-attached
meta-instructions, but for files in less-common languages (e.g.
PowerShell, YAML, SQL), the agent may write without consulting the
matching instruction file. PreToolUse can detect "you're about to
write a `*.ps1` and you have not invoked
`get_autocontext_instructions_file` for
`lang-powershell.instructions.md` yet" and inject a one-line nudge.

**Reuse:**

- Glob matching is
  [InstructionsFilesLmToolsApplyToMatcher](../../src/AutoContext.VsCode/src/instructions-files-lm-tools-apply-to-matcher.ts)
  — the same class the LM-tool list handler uses. Reused via the
  shared CJS seam from Phase 0a.
- Metadata, enabled state, and override flags come from the daemon
  via `Instructions.List` (one round-trip per hook invocation).
  The hook never reads `.autocontext.json`,
  `resources/instructions-files.metadata.json`, or any file under
  `plugin/instructions/` (which doesn't exist) — those are owned
  by the daemon's `InstructionsCorpusService`.
- The `ALWAYS_ATTACHED_INSTRUCTIONS_FILES_SET` constant is the
  initial-ledger contents — one source of truth for "already
  attached".
- Disabled-instruction state and override state are surfaced by
  `Instructions.List` directly (each entry carries `enabled` and
  `overridden` flags). The reminder lookup skips disabled entries
  and still fires for overridden ones — the override is still an
  instruction file the model should consult; only the projected
  body the model later fetches differs.

**Steps:**

- **Step 5.1 — `SessionLedger`.** Class persisting per-session
  "already-read instruction files" state to a temp file keyed by the
  hook's `session_id`. Test:
  `tests/unit-tests/hooks/session-ledger.test.ts`.
- **Step 5.2 — `PreToolUseInstructionLookup`.** Class composing
  `ApplyToMatcher` + `AutoctxClient.instructions.list()` to map a
  write-target path to the instruction-file names that should have
  been read first. Skips non-source artefacts (lock files,
  binaries), entries already in the ledger, and entries reported
  as disabled by the daemon. Test:
  `tests/unit-tests/hooks/pre-tool-use-instruction-lookup.test.ts`.
- **Step 5.3 — `PreToolUseHookRunner`.** Subclass of `HookRunner`.
  Composes the ledger + lookup; emits `additionalContext` only when
  there is something unread. Also subscribes (best-effort) to
  `Instructions.Subscribe` for the duration of the hook to
  re-materialise the SessionStart cache when the daemon reports a
  config change — mitigates Claude-host mid-session staleness. Test:
  `tests/unit-tests/hooks/pre-tool-use-hook-runner.test.ts`.
- **Step 5.4 — Wire into manifest.** Add `PreToolUse` to
  `plugin/hooks/hooks.json`. Test:
  `tests/smoke-tests/pre-tool-use-hook.test.ts`.

**Docs:**

- `walkthroughs/instructions.md` — explain the reminder behaviour.
- `docs/architecture.md` — PreToolUse subsection.

**Risks:**

- **Premature reminder noise.** Always-attached files are seeded into
  the ledger at session start (Step 5.1); reminding for them would be
  redundant.
- **Wrong-direction nag.** Hook fires on writes only and matches the
  write target's path, not the read context.

---

### Phase 6 — `mcpServers` declared in `plugin.json` (cross-IDE)

**One-line:** add the AutoContext MCP server to the plugin manifest
so non-VS-Code Claude clients (Claude Code, Claude Desktop) get the
analyzer tools when they install the plugin folder.

**Why:** today the MCP server is registered through VS Code's
[McpServerProvider](../../src/AutoContext.VsCode/src/mcp-server-provider.ts)
— invisible to other Claude hosts. The plugin spec lets us declare
the same server in `plugin.json`'s `mcpServers` field, making the
analyzer tools available wherever Claude consumes the plugin.

**Reuse:**

- Per-RID `plugin.json` is generated by a new
  `PluginManifestGenerator` class — same pattern and naming as
  [package-instructions-manifest-generator.ts](../../src/AutoContext.VsCode/src/package-instructions-manifest-generator.ts)
  and
  [instructions-files-metadata-generator.ts](../../src/AutoContext.VsCode/src/instructions-files-metadata-generator.ts).
  No new build framework.
- The binary name token (`process.platform === 'win32' ? '.exe' :
  ''`) is already encoded in `McpServerProvider`; the generator
  imports the same helper rather than duplicating the rule.

**Steps:**

- **Step 6.1 — `PluginManifestGenerator`.** New class that reads a
  template `plugin.json` (committed) plus the target RID and writes
  the per-RID `plugin.json` into the staged plugin folder. Test:
  `tests/unit-tests/plugin/plugin-manifest-generator.test.ts`.
- **Step 6.2 — Hook into `Build-VscePackage`.** Generator runs after
  `.NET` publish, before VSIX assembly. Test:
  `tests/smoke-tests/plugin-manifest-generator.test.ts` (drives the
  generator across all supported RIDs against fixture inputs).
- **Step 6.3 — Cross-host smoke.** A test that boots
  `AutoContext.Mcp.Server[.exe]` directly with no `--service`
  arguments and asserts it advertises the analyzer tools without
  crashing.

Example generated manifest (win32-x64 VSIX):

```jsonc
{
    "name": "autocontext",
    "description": "...",
    "author": { "name": "2site.net" },
    "mcpServers": {
        "autocontext-mcp-server": {
            "command": "${CLAUDE_PLUGIN_ROOT}/cli/win-x64/autoctx.exe",
            "args": ["service", "mcp://plugin"]
        }
    }
}
```

The `command` resolves through the same per-RID `cli/<rid>/autoctx`
layout that Phase 0a.4 established for the daemon — one bundled
binary serves both roles. `autoctx service mcp://...` (per
[autoctx-cli.md](./autoctx-cli.md)) is the documented launcher for
the MCP server in standalone mode.

**Critical constraint:** the embedded VS Code path keeps using
`McpServerProvider` so the existing `--service log=…`,
`--service health-monitor=…`, `--service worker-control=…`,
`--service extension-config=…` channels remain intact. The plugin
manifest only registers the server for **non-VS-Code hosts**.
Coordinate with
[mcp-tool-registration-suppression.md](mcp-tool-registration-suppression.md):
when the suppression flag is implemented, the embedded server
suppresses tool registration; the plugin-launched server remains
the canonical surface for non-VS-Code clients.

**Docs:**

- `README.md` — add a one-liner that AutoContext analyzer tools are
  available outside VS Code via Claude plugin install.
- `docs/architecture.md` — update the Bundled Agent-Plugin section
  with the cross-IDE MCP path.

**Risks:**

- **Path resolution.** Resolved — the bundled
  `${CLAUDE_PLUGIN_ROOT}/cli/<rid>/autoctx` layout is
  plugin-relative and host-installation-agnostic, so the manifest
  works wherever the plugin folder lands. Phase 0a.4 ships the
  same binary for the daemon, so there is one source for the
  binary and one packaging step that validates it (Phase 3).
- **Tool duplication inside VS Code** until the suppression flag
  lands. Phase 6 is **gated on**
  [mcp-tool-registration-suppression.md](mcp-tool-registration-suppression.md).

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

Phases 1 and 5 add new `.cts` files. Phase 0a does the markdown
move (no compile step). Phases 2 and 4 add only static markdown.

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

Phase 3 changes the build pipeline to enforce the canonical layout;
no host-visible behaviour change.

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

## Documentation Touch List

| Doc | Phase(s) | What changes |
|-----|----------|--------------|
| `README.md` (root) | 6 | One-liner that AutoContext analyzer tools are available outside VS Code via Claude plugin install. |
| `src/AutoContext.VsCode/README.md` | 0a, 3 | Bundled `autoctx` CLI + daemon-client hook model + packaging guarantees. |
| `docs/architecture.md` | 0a, 1, 2, 4, 5, 6 | New "Bundled Agent-Plugin" section, updated incrementally per phase. |
| `walkthroughs/welcome.md` | 2 | Slash-command teaser. |
| `walkthroughs/tools.md` | 1 | Automatic analyzer chaining after writes. |
| `walkthroughs/instructions.md` | 4, 5 | Sub-agent presets + PreToolUse reminder. |

Docs land in the same step that ships the surface they describe —
never as a separate doc-only commit.

## Files Likely Touched (across all phases)

```
src/AutoContext.VsCode/plugin/.claude-plugin/plugin.json     (Phase 6 — generated)
src/AutoContext.VsCode/plugin/hooks/hooks.json               (Phases 1, 5)
src/AutoContext.VsCode/plugin/commands/<name>.md             (Phase 2 — new)
src/AutoContext.VsCode/plugin/agents/<name>.md               (Phase 4 — new)
src/AutoContext.VsCode/plugin/cli/<rid>/autoctx[.exe]        (Phase 0a — bundled per-RID, copied at package time)
src/AutoContext.VsCode/plugin/scripts/                       (generated, all phases)
src/AutoContext.VsCode/src/hooks/hook-runner.cts             (Phase 0a — new)
src/AutoContext.VsCode/src/hooks/plugin-root-resolver.cts    (Phase 0a — new)
src/AutoContext.VsCode/src/hooks/workspace-context-resolver.cts (Phase 0a — new)
src/AutoContext.VsCode/src/hooks/autoctx-binary-resolver.cts (Phase 0a — new)
src/AutoContext.VsCode/src/hooks/session-start-hook-runner.cts (Phase 0a — refactor; daemon client + cache materialiser)
src/AutoContext.VsCode/src/instructions-files-manager.ts     (Phase 0a — refactor: daemon client; rewrites <extensionPath>/instructions.cache/<hash>/)
src/AutoContext.Framework.Web/src/cli/autoctx-client.ts      (Phase 0a — new; shared pipe RPC client)
src/AutoContext.VsCode/src/hooks/post-tool-use-router.cts    (Phase 1 — new)
src/AutoContext.VsCode/src/hooks/post-tool-use-hook-runner.cts (Phase 1 — new)
src/AutoContext.VsCode/src/hooks/pre-tool-use-instruction-lookup.cts (Phase 5 — new)
src/AutoContext.VsCode/src/hooks/pre-tool-use-hook-runner.cts (Phase 5 — new)
src/AutoContext.VsCode/src/hooks/session-ledger.cts          (Phase 5 — new)
src/AutoContext.VsCode/src/plugin-command-validator.ts       (Phase 2 — new)
src/AutoContext.VsCode/src/plugin-assets-validator.ts        (Phase 3 — new)
src/AutoContext.VsCode/src/sub-agent-definition-validator.ts (Phase 4 — new)
src/AutoContext.VsCode/src/plugin-manifest-generator.ts      (Phase 6 — new)
build.ps1                                                    (Phases 0a, 1, 3, 5, 6)
src/AutoContext.VsCode/tests/unit-tests/hooks/               (Phases 0a, 1, 5)
src/AutoContext.VsCode/tests/unit-tests/plugin/              (Phases 2, 3, 4, 6)
src/AutoContext.VsCode/tests/smoke-tests/                    (Phases 0a, 1, 3, 5, 6)
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
  — Phase 0a source list.
- [src/AutoContext.Mcp.Server/mcp-workers-registry.json](../../src/AutoContext.Mcp.Server/mcp-workers-registry.json)
  — Phase 1 and Phase 2 reference this for analyzer name + parameter
  shape.

## Status

- **Phase 0 (shipped):** SessionStart hook (free-function form),
  plugin scaffolding, installer.
- **Phase 0a (prep, prerequisite):** convert hooks into `autoctx`
  daemon clients; bundle `autoctx` in the VSIX. Lands before
  Phase 1. Itself gated on the CLI plan's daemon slice.
- **Phases 1–6:** not started. Each ships independently when it
  reaches the top of the queue.
