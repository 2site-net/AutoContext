# Plan: `autocontext-engine` (per-workspace state owner and central process)

## Motivation

Today AutoContext spreads its state across two homes:

1. **`AutoContext.Mcp.Server`** is the MCP-protocol facade Copilot talks
   to. Internally it is already a pipe orchestrator — it dispatches to
   workers over named pipes and dials four sideband pipes back to the
   VS Code extension (`log`, `health-monitor`, `worker-control`,
   `extension-config`) to pick up state the extension owns.
2. **The VS Code extension** owns the live state — `.autocontext.json`,
   the curated instructions corpus, projection (file-level and
   rule-level disable), workspace detection, override resolution — and
   re-projects it into static artefacts (`instructions/.generated/`,
   per-window staging dirs) and pushes it to the orchestrator over the
   four sideband pipes.

That topology made sense when VS Code was the only host. It does not
scale. The Anthropic Claude plugin needs the same view — same config,
same projected instructions, same disable-rule semantics — and there
is no longer a single host that can act as the source of truth for
the others. Running the projection logic a second time inside the
plugin would fork the source of truth; a SessionStart hook reading
disk artefacts the extension wrote would couple the plugin to one
specific host being installed.

The fix: pull the state out of the VS Code extension and into a single
.NET process that **every host is a client of**. That process is
`autocontext-engine`.

## Topology — three clients, one engine

```
                   .autocontext.json   instructions/   workspace files
                            \              |              /
                             \             |             /
                            +-------------------------------+
                            |      autocontext-engine       |
                            |   (one process per workspace) |
                            |                               |
                            |  Config · Instructions ·      |
                            |  Workspace · MCP Tools ·      |
                            |  Worker dispatch ·            |
                            |  pipe RPC + MCP/stdio facade  |
                            +-------------------------------+
                              ^         ^         ^         ^
                              |         |         |         |
                              |         |         |         +--- AutoContext.Worker.* (spawned)
                              |         |         |
                              |         |         +--- Agent plugin (hooks)
                              |         |              (consumer; runs under any hook host —
                              |         |               Claude Code, VS Code Copilot, …)
                              |         |
                              |         +--- VS Code extension
                              |              (UI surface; toggles files & rules)
                              |
                              +--- autocontext CLI
                                   (debug & scripting client; see autocontext-cli.md)
```

Three clients, three jobs:

- **VS Code extension** is the **UI surface** for the engine. The user
  toggles instruction files on/off, disables individual rules, exports
  workspace overrides, and watches state in the Instructions /
  MCP Tools tree views — all by issuing RPCs against the engine. The
  extension is a pure RPC consumer: it never writes a projected
  instruction file to disk. Instruction delivery to the chat surface
  is the agent plugin's job, not the extension's — even when the
  chat surface lives inside the same VS Code window.
- **Agent plugin (hooks)** is a **consumer**. The plugin ships two
  hooks today, both wired to the engine over the per-workspace pipe:
  - **`SessionStart`** — calls `Instructions.GetAlwaysAttached` and
    emits the bodies as `additionalContext` so the always-attached
    rules apply to every turn (see issue addressed in the
    `Instructions.*` RPC list below).
  - **`UserPromptSubmit`** — calls `Discovery.RouteForPrompt(prompt)`
    (see RPC surface below) and emits the result as a discovery
    preamble naming the strongly-relevant MCP tools and instruction
    files for this turn.

  Disabled state is opaque to the plugin: a disabled file is simply
  not returned, a disabled rule is simply not in the body. The plugin
  never sees `.autocontext.json`, never resolves overrides, never
  strips `[INSTxxxx]` tags. The plugin uses Anthropic's Claude Code
  hooks format on disk (`hooks/hooks.json` + `.cjs` scripts), but it
  is not Claude-only: any hook host runs it. Today that means
  **Claude Code** and **VS Code Copilot**; future hosts that read the
  same format inherit support for free.

  The remaining standard hook events (`Stop`, `SubagentStart` /
  `SubagentStop`, `PreCompact`, `PreToolUse`, `PostToolUse`) are
  not implemented today but are explicitly **in scope for the
  engine design** — each one is an agent-loop transition the
  engine cannot observe on its own, and surfacing them through the
  engine lets every client (the extension's tree views, the CLI,
  future hosts) react uniformly. Sketch of the role each plays:

  - **`SubagentStart` / `SubagentStop`** — the natural place to
    materialise instruction *files* on disk for sub-agents that
    need a `Uri` rather than inline context. The hook calls
    `Instructions.GetAlwaysAttached` (and a future task-scoped
    `Discovery.RouteForPrompt(subagentTaskPrompt)`), writes the
    bodies under
    `%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\cache\subagents\<sessionId>\`
    (POSIX equivalent), and notifies the engine via
    `Agent.SubagentStarted(sessionId, taskPrompt)`. `SubagentStop`
    deletes the directory and calls `Agent.SubagentStopped(sessionId)`.
    The engine re-broadcasts both events on `Agent.Events.Subscribe`
    so the VS Code tree view can show "active sub-agents: 2" with
    a drill-down listing each session's task and materialised file
    set — giving the user cross-host observability the chat
    surface alone does not provide.
  - **`PreCompact`** — the agent host is about to drop conversation
    history. Always-attached instructions and the static discovery
    preamble must survive compaction; the hook re-injects both
    immediately after the compact event by calling
    `Instructions.GetAlwaysAttached` again and re-emitting the
    `additionalContext` block. The hook also signals
    `Agent.Compacted(sessionId)` so the engine can mark any
    session-scoped derived state (sub-agent file caches, routing
    history) for re-evaluation.
  - **`PreToolUse`** — fires immediately before the agent invokes
    a tool. The hook calls `Discovery.RouteForTool(toolName)`
    (a `Discovery.*` extension keyed by tool identity rather than
    prompt text) and emits any tool-gated instruction file that
    `applyTo` would target — e.g. invoking `analyze_csharp_code`
    surfaces the C# coding-standards instructions, invoking a
    git-commit-analysis tool surfaces the commit-message
    instructions. This catches turns the prompt-text router
    misses (the user said "fix this" but the agent picked a C#
    analyzer).
  - **`PostToolUse`** — fires after a tool returns. Two roles:
    (a) when the tool wrote to `.autocontext.json` or to a file
    under `<workspace>/.github/instructions/`, the hook calls
    `Engine.Reload()` synchronously so the next turn sees the new
    state without waiting for a debounced FS-watch event;
    (b) the hook signals `Agent.ToolUsed(toolName, outcome)` to
    the engine, which folds it into a per-session usage histogram
    available via `Diagnostics.Run` for "which tools did this
    session actually use" reports.
  - **`Stop`** — fires when the agent finishes its turn. The hook
    flushes any session-scoped client cache it owns, signals
    `Agent.TurnEnded(sessionId)` to the engine, and releases any
    keep-alive grip the hook held on the engine pipe so the
    idle-timeout countdown can begin if no other client is
    connected.

  Common shape: hook scripts stay thin composers of existing RPCs
  for *content* (`Instructions.*`, `Discovery.*`, `McpTools.*`,
  `Config.*`, `Workspace.*`); the only new RPC family these hooks
  introduce is **`Agent.*`** — a small set of fire-and-forget
  notifications turning hook events into engine-broadcast signals
  via `Agent.Events.Subscribe`. The engine itself never observes
  the agent loop; the hook is the only sensor, and `Agent.*` is
  how that sensor's reading reaches every other client. This
  preserves the rule "engine adds RPCs only when a hook needs
  state the engine does not already expose" — agent-loop
  transitions *are* such state, so they get RPCs; everything else
  composes existing surfaces.
- **`autocontext` CLI** is the **debug & scripting client**. Same wire
  protocol; standalone invocations for repros, CI, and developer
  troubleshooting without an editor host. See [autocontext-cli.md](./autocontext-cli.md).

When VS Code Copilot runs the agent-plugin hooks alongside the
AutoContext extension in the same window, the two surfaces are
**independent** clients of the same engine, not nested layers. The
hook process talks to the engine directly, not via the extension —
the extension neither launches the hook nor proxies its RPCs.

## At a glance — reference index

A one-screen catalogue of every named entity in this design.
Entries are terse pointers; the authoritative definition lives in
the linked section below. New entities added to the design must
also land here so the index stays the system's table of contents.

### Binaries and processes

| Name | Kind | Scope | See |
|---|---|---|---|
| `autocontext-engine` | .NET binary | one process per (workspace, launcher instance) | [Engine binary](#engine-binary) |
| `autocontext` | .NET CLI binary | one invocation per command; spawns its own engine when needed | [autocontext-cli.md](./autocontext-cli.md) |
| `AutoContext.Worker.DotNet` / `.Workspace` / `.Web` | .NET / Node task workers | spawned lazily by the engine via `WorkerManager` | [What the engine absorbs](#what-the-engine-absorbs-from-todays-topology) |
| `AutoContext.Mcp.Server` | retired in this plan | absorbed into the engine | [What the engine absorbs](#what-the-engine-absorbs-from-todays-topology) |

### Distributed bundle layout

The **shipped** shape of an engine bundle inside any host artefact
(VSIX, plugin root, GitHub-released tarball). This is the runtime
filesystem the engine resolves against via `AppContext.BaseDirectory`
— not the source-tree layout under `src/`, and not the multi-RID
build-output tree under `out/`. Each shipped artefact targets one
platform (one VSIX per platform via `vsce package --target <target>`,
one plugin release per platform, one GitHub-release tarball per RID),
so the per-RID segment that exists in build staging is **absent**
from the shipped product:

```
engine/
  autocontext-engine[.exe]               # engine binary
  <framework dlls / runtime files>       # self-contained .NET runtime for the engine
  Instructions/                          # curated corpus (read-only side-car)
  Resources/                             # build-generated read-only manifests
  Workers/<id>/<entrypoint>              # one self-contained subdir per worker
```

At build-output staging time the layout is
`out/engine/<rid>/{autocontext-engine, runtime, Instructions/, Resources/, Workers/}`
with one subtree per supported RID (`win-x64`, `win-arm64`,
`linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`); per-platform
packaging picks the matching `<rid>/` and copies its contents into
`engine/` in the shipped artefact. The `autocontext` CLI is **not** in
this tree — it ships in its own bundle and nests its own copy of
`engine/` as a side-car (see [autocontext-cli.md](./autocontext-cli.md)).

See [Distribution](#distribution) for per-file roles, manifest
shapes, and host-side resolution rules.

### Engine CLI switches

| Switch | Required | Notes |
|---|---|---|
| `--workspace <path>` | yes | absolute workspace path (P4) |
| `--instance-id <uuid>` | yes | launcher-minted UUIDv4 (P4) |
| `--instance-label <text>` | no | freeform observability descriptor (≤ 200 printable-ASCII) |
| `--idle-timeout <seconds>` | no | default `300` |
| `--retention <duration>` | no | housekeeping retention window; default `1d` |
| `--logging <verbosity>` | no | `normal` (default) — rotate at 1,000 lines OR 5 MB; `debug` — rotate at 5,000 lines OR 25 MB |
| `--mcp-server <mode>` | no | `with-stdio` (only value today) |
| `--version` | no | RID-independent |

See [Engine options (CLI surface)](#engine-options-cli-surface).

### `autocontext` CLI surface

| Verb | Purpose |
|---|---|
| `--version` | print CLI version and exit |
| `config get\|toggle …` | read / mutate `.autocontext.json` via `Config.*` RPCs |
| `instructions list\|get\|watch\|search …` | read corpus via `Instructions.*` RPCs |
| `workspace detect\|info` | `Workspace.*` RPCs |
| `engine status\|logs …` | dial `health` / `logs` pipes directly |
| `mcp invoke <tool> --args <json>` | pipe-side `McpTools.Invoke` |

See [autocontext-cli.md](./autocontext-cli.md).

### Pipes

Name shape: `autocontext-engine:<kind>@<workspaceHash>#<instanceId>`
where `<workspaceHash>` = `sha256(normalisedWorkspacePath):0..16`,
`<instanceId>` = launcher-minted UUIDv4. Four pipes per
(workspace, launcher instance):

| Kind | Keep-alive | Handshake | Payload | Typical clients |
|---|---|---|---|---|
| `rpc` | yes | `Engine.Hello` required | length-prefixed JSON-RPC frames | extension, hooks, CLI |
| `events` | yes | `Hello` envelope required | broadcast envelopes (`Engine.Lifecycle`, future) | every cache-invalidating client |
| `health` | no | none | one small status JSON document | spawners, `autocontext engine status` |
| `logs` | no | none | NDJSON record stream (one record per line) | `autocontext engine logs --follow`, ad-hoc tailers |

See [Lifecycle](#lifecycle) and [P4](#p4-workspace-identity-is-one-hash-engine-identity-adds-one-uuid).

### On-disk paths and ownership

Every path AutoContext touches has exactly one owner (P5).

| Path | Owner | Lifetime |
|---|---|---|
| `<workspace>/.autocontext.json` | engine | workspace; cross-instance shared on disk |
| `<workspace>/.github/instructions/<name>.instructions.md` | user | workspace; overrides bundled |
| `<host-bundle>/engine/{autocontext-engine, Instructions/, Resources/, Workers/}` | build | read-only at runtime |
| `…\autocontext\<workspaceHash>#<instanceId>\logs\engine.log` | engine | rotated in-process by `--logging` thresholds; rotated files retained per `--retention` |
| `…\autocontext\<workspaceHash>#<instanceId>\logs\errors.log` (future) | engine | unhandled-exception / fatal-startup sink |
| `…\autocontext\<workspaceHash>#<instanceId>\logs\worker-<workerId>.log` | engine | one file per spawned worker; records routed by `category` prefix; same rotation + retention rules as `engine.log` |
| `…\autocontext\<workspaceHash>#<instanceId>\cache\<client>\…` | client | client-managed |
| `…\autocontext\engine-metadata.json` | every live engine (co-owned) | row-per-instance liveness registry |

`…` = `%LOCALAPPDATA%\autocontext\` on Windows, `$XDG_CACHE_HOME/autocontext/`
or `~/.cache/autocontext/` on POSIX.

See [P4](#p4-workspace-identity-is-one-hash-engine-identity-adds-one-uuid)
/ [P5](#p5-on-disk-path-ownership-is-explicit-and-exclusive).

### RPC surface

Grouped by namespace (handler families live in the engine; transports
are marshalling shims — P1).

| Namespace | Methods |
|---|---|
| `Engine.*` | `Hello`, `GetSharedMetadata`, `WriteLog` (fire-and-forget from workers), `Lifecycle.Subscribe` |
| `Config.*` | `Get`, `Subscribe`, `ToggleFile`, `ToggleRule` |
| `Instructions.*` | `List`, `Get`, `GetAll`, `GetAlwaysAttached`, `GetRaw`, `SearchContent`, `Subscribe` |
| `Workspace.*` | `Detect`, `Info` |
| `Logs.*` | `GetEngine`, `TailEngine`, `GetWorker`, `TailWorker` |
| `McpTools.*` | `List`, `Invoke` (future: `InvokeStream`, `GetDescription`, `SearchByMetadata`, `SearchByContent`) |
| `Discovery.*` | `RouteForPrompt`, `RouteForTool` |
| `Agent.*` | `SubagentStarted`, `SubagentStopped`, `Compacted`, `ToolUsed`, `TurnEnded` (all fire-and-forget notifications), `Events.Subscribe` |

State-bearing reads return discriminated envelopes
(`ok` / `disabled` / `not-found` / `*-error`) — P2.
See [RPC surface (initial)](#rpc-surface-initial).

### Wire envelopes (one-line shapes)

| Envelope | Shape |
|---|---|
| Log record (engine + worker, on `logs` pipe, in `engine.log` and `worker-<workerId>.log`) | `{ timestamp, category, level, eventId?, message, properties?, exception? }` |
| `Engine.GetSharedMetadata` row | `{ workspaceHash, instanceId, instanceLabel, pid, processStartTimeUtc, engineVersion, startedAt, retention }` |
| `Instructions.List` row | `{ key, fileName, name, version, description, applyTo?, hasChangelog, contentHash, alwaysAttached, disabled, source, overridePath?, sections? }` |
| `Instructions.Get` response | `\|` of `{ kind: "ok", … }` / `{ kind: "disabled", … }` / `{ kind: "not-found", … }` |
| `McpTools.Invoke` response | `\|` of `{ kind: "ok" \| "tool-error", content, isError? }` / `{ kind: "schema-error", errors[] }` / `{ kind: "disabled" \| "not-found" }` |
| `Workspace.Detect` | `{ flags: { hasDotNet, hasCSharp, …~60 }, extensions[], overrides: { paths[], names[] } }` |

### Log categories (prefix taxonomy, convention not closed enum)

| Prefix | Producer |
|---|---|
| `engine.rpc.<Handler>` | engine RPC handlers |
| `engine.events` | engine `Lifecycle` / `Agent.Events` broadcast |
| `engine.health` | engine health-pipe handler |
| `engine.lifecycle` | engine startup / shutdown / idle transitions |
| `engine.startup` | argv parse, pipe bind, manifest load |
| `engine.logging` | log pipeline's own diagnostics (drops, slow-subscriber evictions) |
| `worker.<workerId>.engine.stderr` | captured worker stderr from the engine's supervision channel |
| `worker.<workerId>` | worker root (`worker.dotnet`, `worker.workspace`, `worker.web`) |
| `worker.<workerId>.<Type>` | per-type sub-category under a worker |

See [Log categories](#log-categories).

### Engine-internal services (.NET, under `AutoContext.Engine/`)

| Service | Role |
|---|---|
| `AutoContextConfigStore` | owns `.autocontext.json`, validates and broadcasts writes |
| `InstructionsCorpusService` | corpus + per-request projection |
| `InstructionsFileBodyProjector` | raw → projected body (disabled-rule filter, `[INSTxxxx]` strip, override resolution) |
| `InstructionsListBuilder` | build-time manifest generator and startup ingestion |
| `InstructionsContentIndex` | in-memory content-search index (replaces extension-side trigram index) |
| `WorkspaceContextDetector` | workspace detection (absorbed from extension) |
| `WorkerManager` | lazy `ensureRunning(workerId)` worker dispatcher (absorbed from MCP server) |

### Composition seams

| Seam | Layer |
|---|---|
| `IHostApplicationBuilder.AddAutoContextEngine(Action<EngineOptions>)` | engine library's single public entry; CLI and tests both call it |
| `EngineOptions` | CLI-surfaced knobs + library-only knobs (corpus root override, pipe-name override) |
| `AddEngineLoggerProvider()` (in `AutoContext.Worker.Shared`) | worker-side logging seam routing `ILogger<T>` to `Engine.WriteLog` |
| `AutoctxClient` (TS, `Framework.Web/src/cli/`) | only shared TS class; pipe-RPC client used by extension and hooks |

See [Composition contracts](#composition-contracts).

### Build-generated `Resources/` manifests (per-RID, read-only at runtime)

| File | Role |
|---|---|
| `instructions-files.json` | wire-shape catalogue for `Instructions.List` |
| `instructions-files-metadata.json` | engine-internal indices (section maps, parsed `applyTo` extension sets, content-index seed) |
| `mcp-tools.json` | wire-shape catalogue for `McpTools.List` |
| `mcp-tools-registry.json` | source-of-truth tool→worker dispatch table (hand-edited) |
| `mcp-tools-registry-schema.json` | JSON-schema for the registry (hand-edited) |
| `workers.json` | build-generated worker manifest (id + type + entrypoint per worker) |

See [Resource manifests](#resource-manifests).

### Design principles (cross-cutting)

| Id | Rule |
|---|---|
| **P1** | One handler per capability; transports are marshalling shims |
| **P2** | Discriminated envelopes for state-bearing reads (`ok` / `disabled` / `not-found` / `*-error`) |
| **P3** | Wire shape ≠ engine-internal shape (split build-generated manifests in two) |
| **P4** | Workspace identity is one hash; engine identity adds one launcher UUID |
| **P5** | On-disk path ownership is explicit and exclusive |
| **P6** | Subscriptions are first-class; clients never poll or watch |
| **P7** | Two-layer matching: coarse on the producer, fine on the consumer |

See [Design principles (cross-cutting)](#design-principles-cross-cutting).

## What the engine absorbs from today's topology

The engine is the new home for everything that today is split between
`AutoContext.Mcp.Server` and the VS Code extension's pipe-server
classes:

| Today | Lives in | Becomes |
|-------|----------|---------|
| `AutoContext.Mcp.Server` (orchestrator + MCP/stdio + worker dispatch + registry) | Standalone process | **Engine internal**; MCP/stdio is one outward transport on the engine |
| `AutoContextConfigManager` (TS, extension) | Extension process | **Engine internal**: `AutoContextConfigStore` (.NET) |
| `InstructionsFilesManager` + `InstructionsFileContentProjector` + `instructions-files-metadata-generator` + client-side content trigram index | Extension process | **Engine internal**: `InstructionsCorpusService` + `InstructionsFileBodyProjector` + `InstructionsListBuilder` (now runs **both** at build time — producing `Resources/instructions-files.json` and `Resources/instructions-files-metadata.json` side-car manifests — **and** at engine startup, where the engine reads the manifests, applies per-request projection against workspace state, and returns rows via `Instructions.List`) + `InstructionsContentIndex` (replaces the client-side trigram index; built in-memory from the build-time metadata manifest at engine startup) |
| `servers.json` (TS-side worker/MCP-server inventory) + `mcp-workers-registry.json` (MCP-server–side worker dispatch table) | Extension `resources/` + `AutoContext.Mcp.Server/` | **Replaced** by build-generated `Resources/workers.json` (scan of `src/AutoContext.Worker.*/` projects, id derived by stripping `AutoContext.Worker.` and replacing `.` with `-`, entrypoint written from the actual published path) + `Resources/mcp-tools-registry.json` (renamed from `mcp-workers-registry.json`; tool→worker dispatch table) + `Resources/mcp-tools-registry-schema.json` (its JSON-schema). The old `servers.json` mixed MCP-server identity with worker identity; the MCP server is gone (consolidated into the engine), so the worker-only file is what remains. |
| `LogServer` (sideband pipe) | Extension process | **Engine internal**: the engine binds the `logs` pipe (one of the four pipes — see `### Lifecycle`) as a unified server-streaming sink that fans out engine-emitted records **and** worker-emitted records forwarded through `Engine.WriteLog`, distinguished by the `category` field. The engine also persists every record to `…\<workspaceHash>#<instanceId>\logs\engine.log` (P4 / P5); clients tail the pipe (`autocontext engine logs --follow`) instead of inventing their own log-watcher. |
| `HealthMonitorServer` (sideband pipe) | Extension process | **Engine internal**: the engine binds the `health` pipe (one of the four pipes — see `### Lifecycle`) as a passive readiness/heartbeat probe — cheap connect-and-read, no `Engine.Hello` required, never counts toward the idle-timeout keep-alive gate. Replaces the extension-side `HealthMonitorServer` that earlier topology had clients dialling back to. |
| `WorkerControlServer` (sideband pipe) | Extension process | **Engine internal**: engine spawns workers via the same lazy gate |
| `AutoContextConfigServer` (sideband pipe) | Extension process | **Gone** — config IS engine state; pushes to subscribers via `Config.Subscribe` over the engine pipe |
| `WorkspaceContextDetector` | Extension process | **Engine internal**: workspace detection runs on engine startup; clients read via `Workspace.*` RPCs |
| `instructions/.generated/` (on-disk projection) | Extension's extensionPath | **Gone** — projection happens in-memory in the engine; the extension consumes projected bodies as strings over the pipe |
| `instructions/.workspaces/<hash>/` per-window staging | Extension's extensionPath | **Gone** — no on-disk staging needed |

Workers (`AutoContext.Worker.DotNet`, `AutoContext.Worker.Workspace`,
`AutoContext.Worker.Web`) are **not** absorbed. They stay as separate
binaries with their existing JSON-RPC-over-pipe protocol, spawned by
the engine on demand using the same lazy `ensureRunning(workerId)`
pattern `WorkerManager` uses today.

### What `AutoContext.Framework` carries over

`AutoContext.Framework` is **not deleted; it is the existing .NET
substrate the engine builds on**, with direction-of-flow flipped in
two places where the old extension was the server and the engine is
now. Concretely, by namespace:

- **`AutoContext.Framework.Pipes`** — reused as-is. The pipe transport
  primitives (`PipeListener` / `BoundPipeListener`, `PipeTransport`,
  `LengthPrefixedFrameCodec`, `PipeKeepAliveClient`, and the
  `PipeTransientExchangeClient` / `PipePersistentExchangeClient` /
  `PipeStreamingClient` triad) are the substrate behind the engine's
  four-pipe topology (P4 — `rpc`, `events`, `health`, `logs`). The
  framing layer, ready-marker contract, and back-pressure
  discipline are all already battle-tested by the current
  MCP-server↔worker plumbing; the engine reuses them unchanged.
  `AutoctxClient` (the only shared TS class — see `## Sharing
  principle`) and the engine's own pipe host both sit on top of
  this namespace.
- **`AutoContext.Framework.Logging`** — reused, with the wire envelope
  renamed and extended. Today's `LogEntry` / `JsonLogEntry` carry
  `(Category, Level, Message, Exception, CorrelationId)` and ship
  via `PipeLoggerProvider` / `LoggingClient` to the extension's
  `LogServer`. Under the engine design the direction reverses:
  the engine binds the `logs` pipe and workers ship to it via
  `Engine.WriteLog` (see the *Log categories* subsection and the
  `Engine.WriteLog` RPC). The existing `Category` field becomes
  the canonical `category` taxonomy field, the `CorrelationId`
  field collapses into the `properties` bag, and `JsonLogEntry`
  grows `timestamp` / `eventId?` / `properties?` / `exception?`
  to match the wire envelope documented under `Engine.WriteLog`.
  `PipeLoggerProvider` is the seed for the worker-side
  `AddEngineLoggerProvider()` registration; one rename plus the
  envelope extension and it slots into the new design without a
  rewrite.
- **`AutoContext.Framework.Hosting`** — `HealthMonitorClient` flips
  direction. Today it dials the extension's `HealthMonitorServer`;
  under the engine design the engine binds the `health` pipe (P4)
  and `HealthMonitorClient` becomes the **client** of the engine's
  pipe. Same wire shape (cheap connect-and-read, no `Engine.Hello`
  required), opposite end of the conversation. The class moves
  into the host-side client library (`AutoctxClient` plumbing on
  .NET hosts; equivalent on TS via `Framework.Web`); the
  server-side counterpart lives in the engine's pipe host.
- **`AutoContext.Framework.Workers`** — `WorkerHostOptions` and
  `WorkerTaskDispatcherService` are the worker-side hosting
  scaffold every `AutoContext.Worker.*` project already inherits.
  They stay where they are. The engine's orchestrator-side
  counterpart (today's `WorkerManager` plus the dispatch path in
  `AutoContext.Mcp.Server`) gets absorbed into the engine binary
  per the table above. The framework half remains the worker
  hosting contract.

Net effect: the framework project keeps its purpose as **shared
.NET infrastructure for every AutoContext .NET process** (engine
and workers), the engine just becomes its largest consumer.
Nothing in the namespace is dead; a few wire envelopes get
extended and one client flips direction. No new "portability
interfaces" appear here — this is composition of concrete .NET
types, exactly as `## Sharing principle` requires.

## Engine binary

`autocontext-engine` is a separate .NET binary, distributed inside
each AutoContext host bundle (the VS Code extension's VSIX, the
Anthropic plugin root). It is **not** a subcommand of `autocontext`,
the standalone CLI ([autocontext-cli.md](./autocontext-cli.md)); the CLI
is distributed separately and ships its own per-RID engine copy
when it needs to spawn one. Running the engine and running the
CLI are different processes. A binary is one role.

### Process scoping: one engine per launcher instance per workspace

The engine is **always (workspace, launcher-instance)-scoped**.
`autocontext-engine`'s `--workspace <path>` and `--instance-id
<uuid>` arguments are both mandatory; there is no "daemon-wide"
mode that serves multiple workspaces, and there is no implicit
shared engine across unrelated launchers on the same workspace.
The reasons are structural, not incidental:

- **State is workspace-shaped.** `.autocontext.json`, the override
  directory `<workspace>/.github/instructions/`,
  workspace-context detection results, and the `disabledTools` /
  `disabledTasks` state are all per-workspace. A single process
  serving N workspaces would just be N independent state machines
  glued into one address space — no shared cache, no shared
  lifecycle, only shared crash blast radius.
- **Lifecycle is launcher-shaped.** A *launcher instance* is one
  spawn-decision point — a single VS Code window (extension + the
  hooks VS Code Copilot runs inside it share that window's
  instance), one Claude Code session, one `autocontext` CLI
  invocation. The launcher mints a UUIDv4 once at startup, passes
  it on `--instance-id` when it spawns the engine, and uses the
  same UUID to dial the engine's pipes thereafter. Engines
  idle-timeout when their own launcher's keep-alive clients
  disconnect; an unrelated launcher on the same workspace runs an
  independent engine with an independent idle clock.
- **Pipe naming makes this concrete.** Every pipe name carries both
  identifiers — `autocontext-engine:<kind>@<workspaceHash>#<instanceId>`
  — so the hash identifies the workspace, the UUID identifies the
  launcher instance, and together they identify the engine. See
  [Lifecycle](#lifecycle) > *Pipe name* for the canonical format,
  the four `<kind>` values, and the normalisation rules.

Consequences:

- **`Workspace.Detect`** runs on the engine's own configured
  workspace path — the path passed via `--workspace`. It is not a
  general-purpose "detect any path" RPC. The CLI's
  `autocontext workspace detect [<path>]` resolves `<path>` (or CWD),
  spawns its own engine for that path with its own instance UUID,
  and asks the engine for its detection result. Asking one engine
  to detect a different workspace is not on the wire.
- **A user with three workspaces open in two VS Code windows plus a
  parallel Claude Code session ends up with multiple engine
  processes** — one per (workspace, launcher) pair, each idle-timing
  out independently. This is a feature, not a bug — isolation
  matches both the existing per-workspace MCP server model and the
  natural launcher lifecycle.
- **`Agent.*` cross-broadcast is preserved within a launcher
  instance.** Hooks spawned by a launcher and that launcher's
  primary client (the extension, the Claude session) share the
  same engine, so `Agent.SubagentStarted` from a hook reaches the
  extension's tree view through `Agent.Events.Subscribe` on the
  same engine. Across launchers, agent events stay local — an
  active sub-agent in one VS Code window is invisible to a second
  VS Code window on the same workspace, by design.
- **Cross-instance `.autocontext.json` is shared on disk, not
  over the wire.** Two engines on the same workspace coordinate
  through the file system: writes use `FileShare.None` plus
  exponential-backoff retry; reads pick up peer changes through
  the engine's existing `FileSystemWatcher` and surface as the
  usual `Config.Subscribe` event. Engines never RPC each other —
  the only cross-instance channel is the workspace file system,
  which is also the channel through which any external editor of
  `.autocontext.json` is already observed.
- **Workspace identity is still the path.** Path normalisation
  (resolve symlinks, lowercase on Windows) collapses the
  unintentional multi-engine cases that would otherwise arise
  from path-shape differences alone. The launcher dimension is
  additive: same workspace from two launchers = two engines on
  purpose; same workspace at two different absolute paths = two
  engines by accident, which the normalisation prevents.
- **Instance-id propagation is the launcher's responsibility.**
  Clients that *spawn* the engine mint the UUID and use it
  directly. Clients that need to dial an *already-running* engine
  without being the launcher (a hook script run by an external
  host process, an ad-hoc `autocontext engine logs --follow` from a
  terminal) need to learn the instance-id through a side channel
  the launcher provides — typically an environment variable
  inherited from the launcher process, or a discovery file the
  launcher writes under the OS user-cache root. The exact
  side-channel is out of scope for the engine binary itself; the
  engine only consumes `--instance-id` and bakes it into its pipe
  names. The launcher contracts that propagate it are specified
  per-host (extension, CLI, plugin) in those hosts' own surfaces.

### Lifecycle

- **Pipe topology: four pipes per (workspace, launcher instance).**
  The engine binds four named-pipe servers per (workspace, launcher
  instance) pair, separated by purpose so a slow consumer on one
  transport never back-pressures another. Each
  pipe is its own `NamedPipeServerStream` (Unix domain socket on
  POSIX) accepting many concurrent client connections via
  multi-instance.

  | Kind | Purpose | Keep-alive? | Typical clients |
  |---|---|---|---|
  | `rpc` | Request/response and server-streaming RPC (`Engine.Hello`, `Config.*`, `Instructions.*`, `Workspace.*`, `McpTools.*`, `Discovery.*`, `Agent.*` notifications, `*.Subscribe` channels other than `Engine.Lifecycle`) | **yes** | every functional client (extension, hook scripts, CLI) |
  | `events` | Engine-broadcast lifecycle stream (`Engine.Lifecycle.Subscribe`, future global broadcasts) | **yes** | every client that needs cache invalidation on reload / shutdown |
  | `health` | Passive readiness / heartbeat probe (cheap connect-and-read shape; no `Hello` required) | **no** | spawners deciding "is the engine up?", CLI `autocontext engine status`, future monitoring |
  | `logs` | Server-streaming log tail — unified sink for engine-emitted **and** worker-emitted records, distinguished by the `category` field on every record (see *Log categories* below) | **no** | `autocontext engine logs --follow`, ad-hoc `nc` / `Get-Content` debugging |

  **Why four and not one.** Isolation and separation: a forgotten
  `logs --follow` in a terminal must not pin the engine alive, must
  not back-pressure an `rpc` call, and must not require the consumer
  to speak the rpc handshake. A passive health probe must not require
  protocol-version negotiation. The `events` stream is broadcast-shaped
  (one fan-out, every subscriber sees the same envelope) and survives
  pipe-lifecycle independently of any in-flight `rpc` call. Splitting
  by kind makes each pipe's contract narrow enough to reason about on
  its own — and is the dial-only-what-you-need shape every client
  actually wants (a hook script doing one `Instructions.Get` dials
  only `rpc`; a status tool dials only `health`).
- **Pipe name** is derived deterministically from the absolute
  workspace path plus the launcher-minted instance UUID:
  `autocontext-engine:<kind>@<workspaceHash>#<instanceId>`, with
  `<kind>` ∈ {`rpc`, `health`, `logs`, `events`}, `<workspaceHash>`
  = `sha256(normalisedWorkspacePath):0..16`, `<instanceId>` =
  UUIDv4. Path normalisation: resolve symlinks, lowercase on
  Windows. The workspace hash is one (P4 — one hash, four names
  sharing it within an instance); the UUID is the launcher's,
  passed verbatim to the engine on `--instance-id` and reused on
  every dial. Platform prefix (`\\.\pipe\` on Windows,
  `${os.tmpdir()}/` on POSIX) is applied by the pipe transport, not
  baked into the name.
- **Independent dial.** Clients dial only the pipes they need. The
  VS Code extension dials `rpc` + `events`; a SessionStart hook that
  only wants `Instructions.GetAlwaysAttached` dials `rpc`; the CLI
  `autocontext engine status` dials `health`; `autocontext engine logs
  --follow` dials `logs`. There is no requirement to dial all four,
  and no implicit cross-pipe correlation — each pipe is an
  independent transport. A client that wants invalidation signals
  must explicitly dial `events`; one that doesn't, won't see them.
- **Cold start (try-connect-with-retry, no pre-flight).** A client
  that is also the launcher mints its instance UUID, attempts to
  connect to the pipe it wants (using
  `autocontext-engine:<kind>@<workspaceHash>#<thisInstanceId>`); on
  failure it asks a single spawner abstraction to spawn
  `autocontext-engine --workspace <path> --instance-id <uuid>`
  detached and retries against two budgets, both independent of
  `Engine.Hello`:
  - **Warm connect (no spawn):** sub-second.
  - **Cold connect (after spawn):** up to a few seconds with
    exponential backoff. A self-contained .NET process binding
    its four pipes routinely takes hundreds of milliseconds on
    first launch, more under load. The engine binds all four
    pipes before accepting on any of them, so a successful
    connect on one pipe guarantees the other three are also
    bound — clients never have to retry sibling pipes
    independently.

  No cross-platform pipe-existence pre-flight: existence tests for
  Unix sockets are unreliable; a single try-connect is the canonical
  probe (the cheapest pipe to dial for that purpose is `health`).
- **Concurrent first-connect within a launcher instance.** When
  two clients of the *same* launcher race (e.g. the extension and a
  hook spawned by the same VS Code window, both holding the same
  instance UUID), the spawner serialises and ensures at most one
  engine process actually starts; the loser of the race re-enters
  the connect-retry loop against the winner. A second engine
  process that does manage to start with the same `--instance-id`
  must detect existing pipes on bind (any of the four colliding is
  enough) and exit cleanly (**idempotent bind**). Two launchers on
  the same workspace dial different pipe names (different
  `<instanceId>` suffix) and start independent engines by design —
  this is not a race, it is two engines.
- **Wire-protocol handshake.** `Engine.Hello` is an `rpc`-pipe RPC.
  After connecting `rpc`, the client issues `Engine.Hello` *before*
  any other RPC, capped by an independent short budget. `events` requires
  the same handshake (first frame is a `Hello` envelope sharing
  the same protocol-version integer) so a misversioned client cannot
  silently subscribe to a stream it would misparse. `health` and
  `logs` do **not** require `Hello` — both are passive read-shaped
  surfaces whose payload shape is **additively versioned**: `health`
  emits one small status JSON document, `logs` emits one structured
  log record per line (NDJSON; the record schema is documented under
  the `Engine.WriteLog` RPC and the *Log categories* subsection).
  New fields may appear on either payload over time, but consumers
  parsing well-formed JSON and ignoring unknown fields stay
  forward-compatible without protocol-version negotiation — which is
  the actual stability property the no-`Hello` rule needs, and the
  reason these two pipes can skip the handshake the framed `rpc` and
  `events` pipes require.
  The protocol version is an integer constant bumped on every
  wire-format change. **Compat rule: exact-match required.** Engine
  and client must agree on the integer; mismatch in either direction
  refuses. Each host ships its own bundled `autocontext-engine`
  (inside the VSIX, inside the Claude plugin root), and the release
  process versions hosts together — a handshake mismatch in
  production is a packaging bug, not a scenario the protocol tries
  to recover from. Clients surface the refusal as a hard error
  (CLI exit 69, hook structured failure); we do not try to negotiate
  down.
- **Warm reuse within a launcher instance.** Subsequent clients of
  the *same* launcher (the extension and the hooks running in the
  same VS Code window, a long-lived Claude session and a one-shot
  diagnostic dialled by a hook script with the same instance UUID)
  connect to the existing engine's pipes. State is consistent
  across all of them — the four pipes share one engine process,
  one in-memory state store, one generation counter. A *different*
  launcher (a second VS Code window on the same workspace, a
  parallel Claude Code session) starts its own engine and gets its
  own in-memory state; the only state they share is what lives on
  disk in the workspace.
- **Idle shutdown.** The engine exits after `--idle-timeout` seconds
  with no **keep-alive clients** connected (default 300), with a
  fixed **2-second grace period** after the last keep-alive
  disconnect to absorb VS Code reload churn (extension-host
  restart, language-service refresh). Only `rpc` and `events`
  connections count toward the keep-alive gate; `health` and `logs`
  are passive observers and do **not** pin the engine alive — a
  forgotten `logs --follow` terminal or a polling monitor on
  `health` cannot prevent idle shutdown. When the gate fires while
  `health` or `logs` clients are still connected, the engine emits
  `shuttingDown` on `events` (for any subscribers there), closes
  all four pipes, and exits; passive observers see a clean EOF.
- **Crash recovery.** Stale pipe handles surface through the same
  try-connect-with-retry path: a failed connect is treated as "engine
  absent" and triggers a respawn. Because the four pipes are bound
  together by one process, a stale-on-one is stale-on-all — the
  respawn replaces the whole quartet atomically.
- **MCP/stdio facade.** When launched by an MCP host (VS Code's MCP
  manager, Claude Desktop's MCP config) with `--mcp-server with-stdio`,
  `autocontext-engine` exposes the MCP protocol over stdin/stdout
  *as well as* serving its four workspace pipes to other clients. All
  transports share state. The active MCP/stdio connection counts
  toward the keep-alive gate the same way `rpc` does — losing stdio
  is treated as a regular keep-alive disconnect for idle-timeout
  purposes; `rpc` / `events` clients on the pipe side keep the
  engine alive on their own. Without `--mcp-server`, the engine
  never registers an MCP transport and leaves its own stdin/stdout
  untouched — non-MCP spawners (extension, agent plugin, `autocontext`
  CLI) launch the engine with `stdio: 'ignore'` precisely so the
  SDK's read loop can't hit immediate EOF on a `/dev/null` stdin
  and self-terminate the process.

### Housekeeping

The engine self-manages every on-disk artefact it produces, on a
two-clock schedule: a **startup sweep** runs before the engine
binds pipes, and a **shutdown sweep** runs after the engine
removes its own registry row. No external sweeper, no periodic
while-alive timer — every spawn of any engine on the machine pays
the housekeeping cost on behalf of every dead peer, which scales
automatically with how often engines actually run.

- **Startup sweep (mandatory).** After writing its own registry
  row but before binding pipes, the engine enumerates every row
  in `…\autocontext\engine-metadata.json` plus every sibling
  `…\autocontext\<workspaceHash>#<instanceId>\` directory under
  the autocontext root, and classifies each. Live-row classification
  takes precedence over directory-presence checks — a live row's
  subtree may not yet exist (the engine writes its row before
  creating its subtree) and that is normal, not a sweepable state:
  - **Live row** (`pid` exists AND `Process.StartTime` ≈
    `processStartTimeUtc` within ~1 s tolerance): skip, regardless
    of whether the matching subtree exists yet.
  - **Stale row with subtree** (pid missing OR start-time mismatch):
    owning engine is dead. If `now - startedAt` ≥ the row's
    `retention` duration, delete the matching per-instance subtree
    (whole tree — `logs\` + `cache\`) and drop the row; otherwise
    leave both in place and let the next startup re-check.
  - **Stale row without subtree**: subtree was already swept in a
    previous pass (or removed out-of-band) but the row remained.
    Drop the row unconditionally — there is nothing to retain.
  - **Rowless subtree** (directory exists, no matching row): a
    crash before the row was durably flushed, a pre-registry
    leftover, or a registry corruption that lost the row. Use the
    directory's mtime as the timestamp and honour *this engine's
    own* `--retention` (no row = no peer's preference to respect).
- **Shutdown sweep (mandatory, best-effort).** On `AppDomain.ProcessExit`
  / SIGTERM / Windows service-stop, the engine removes its own
  registry row and re-runs the same classification pass against
  remaining peers. Bounded by a short deadline (≤ 1 s) so a slow
  filesystem can't hang shutdown; whatever the sweep doesn't reach,
  the next startup catches. Crash paths skip the shutdown sweep
  entirely — the next engine to start absorbs the work.
- **Retention is per-row.** Each engine writes its `--retention`
  value into its own registry row (see `Engine.GetSharedMetadata`
  shape under `### RPC surface`). A peer sweeping that row honours
  *the dead engine's* declared retention, not its own — a
  long-retention engine can crash and its leftovers stay the
  configured window even if every subsequent engine declares
  `--retention 0`. Rowless subtrees fall back to the sweeping
  engine's own `--retention` (no per-row preference to respect).
- **Concurrency.** Two engines starting near-simultaneously both
  run the startup sweep; both pid-check the *same* peer's row,
  both decide to delete the *same* subtree.
  `Directory.Delete(recursive: true)` under contention is
  best-effort: one engine succeeds, the other sees
  `DirectoryNotFoundException` mid-walk and treats it as
  already-cleaned (no error). Registry-row removal is similarly
  idempotent. The startup ordering — *write own row before
  touching own subtree* — guarantees that any rowless subtree is
  a genuine crash-before-row-flush, not a healthy newborn engine
  one tick behind its sweeper neighbour.
- **Log rotation (within-instance, driven by `--logging`).** The
  engine's own `engine.log` and per-worker `worker-<workerId>.log`
  files rotate in-process by line-count or size threshold:

  | Verbosity | Rotation threshold |
  |---|---|
  | `normal` (default) | 1,000 lines OR 5 MB, whichever fires first |
  | `debug` | 5,000 lines OR 25 MB, whichever fires first |

  When a threshold fires, the engine renames the active file to
  `engine-<iso8601>.log` (and worker equivalents to
  `worker-<workerId>-<iso8601>.log`) and opens a fresh active file.
  `<iso8601>` is the rotation timestamp in UTC with `:` stripped —
  e.g. `engine-20260511T143052Z.log`. Active files always live at
  the stable names (`engine.log`, `worker-<workerId>.log`);
  postmortem readers and `Logs.Get*` / `Logs.Tail*` RPCs read the
  **active file only**, never the rotated history. Rotated files
  are filesystem-inspection artefacts, kept around for `grep` and
  future history-aware tooling.
- **Rotated-file retention.** Rotated logs are subject to the same
  `--retention` window the housekeeping sweep uses. The engine
  prunes its own rotated logs as part of the rotation event
  itself — a cheap scan of its own `logs\` directory looking for
  files matching the rotation pattern whose `<iso8601>` is more
  than `--retention` ago. No separate timer is needed. Rotated
  logs of *dead* peers get swept together with their containing
  per-instance subtree by the cross-instance sweep above; the
  per-file retention check on rotated files only applies to the
  living engine's own subtree.
- **What never gets housekept by the engine.** The shared registry
  file itself (the engine only touches its own row),
  `<workspace>/.autocontext.json`, and
  `<workspace>/.github/instructions/` are outside the per-instance
  cache root and outside housekeeping scope. Client cache subtrees
  under a *live* instance's `cache\` remain client-owned (P5); only
  when the owning instance is verifiably dead does the engine sweep
  delete the whole per-instance subtree, cache and all.

### Engine options (CLI surface)

The engine accepts exactly eight command-line switches; anything
else is rejected at argv parse time with a non-zero exit and a
one-line **stderr** error listing the accepted set (never stdout —
under `--mcp-server with-stdio` stdout is the MCP JSON-RPC channel
and any stray write corrupts it).

| Switch | Required | Value | Default | Set by |
|---|---|---|---|---|
| `--workspace <path>` | yes | absolute workspace path | — | every spawner |
| `--instance-id <uuid>` | yes | UUIDv4 | — | every spawner |
| `--instance-label <text>` | no | short freeform descriptor (≤ 200 printable-ASCII chars, no control chars or newlines) | empty | every spawner that wants observability |
| `--idle-timeout <seconds>` | no | positive integer | `300` | optional override |
| `--retention <duration>` | no | duration string (`<n>{s\|m\|h\|d}`; `0` = sweep immediately) | `1d` | optional override |
| `--logging <verbosity>` | no | `normal` \| `debug` | `normal` | optional override |
| `--mcp-server <mode>` | no | `with-stdio` (the only accepted value today) | off | MCP hosts only |
| `--version` | no | — | — | humans, CI |

Semantics:

- **`--workspace <path>`** is mandatory. The engine pins to one
  workspace; there is no daemon-wide mode and no auto-detection
  from the working directory (P4 — workspace identity is the path,
  not the launcher's CWD).
- **`--instance-id <uuid>`** is mandatory. The launcher mints a
  UUIDv4 once per launcher instance (one VS Code window = one UUID
  shared by the extension and the hooks VS Code Copilot runs in
  that window; one Claude Code session = one UUID; one `autocontext`
  invocation that spawns its own engine = one UUID) and passes the
  same UUID on every spawn and every dial for the life of that
  launcher. The engine validates the value matches the UUIDv4
  shape (lowercase hex, hyphenated) and rejects malformed input;
  it does not interpret the bytes further. The UUID becomes the
  `<instanceId>` segment of every pipe name (see `### Lifecycle`
  > pipe name), which is how clients dial the right engine without
  any runtime discovery: the launcher already knows the UUID it
  minted, so it already knows the full pipe endpoint before the
  engine has even started. Non-launcher clients (a hook running
  under a host process the launcher did not control, an ad-hoc
  `autocontext engine status` from a terminal) learn the UUID through
  a host-specific side channel — this propagation is the
  launcher's responsibility and out of scope for the engine
  binary.
- **`--instance-label <text>`** is an optional, freeform
  human-readable descriptor the launcher attaches to this engine
  instance purely for observability. The convention is a
  semicolon-separated list of `<component> (v<version>)`
  fragments naming the launcher and the engine build it spawned
  — e.g. `vscode (v0.9.5); engine (v0.9.5)` from the VS Code
  extension, `claude-code (v1.2.0); engine (v0.9.5)` from a
  Claude session, `autocontext (v0.9.5); engine (v0.9.5)` from the
  CLI — but the engine treats the value as an opaque string. It
  is validated only for shape (≤ 200 chars, printable ASCII
  only, no control characters, no newlines, no embedded `\r`
  / `\t` that would break structured logging); content beyond
  that is the launcher's choice. The engine captures the label
  into `EngineOptions.InstanceLabel`, attaches it as a structured
  field on every log line (so postmortem reading
  `…\autocontext\<workspaceHash>#<instanceId>\logs\engine.log`
  reveals which host launched the engine without cross-referencing
  the UUID against external state), and surfaces it on the
  `Workspace.Info` RPC and the `health` pipe payload so tree views
  and `autocontext engine status` can render it. The label has **no**
  semantic effect on engine behaviour: it does not appear in pipe
  names, does not appear in on-disk paths, and is never used for
  routing, identity comparison, or compatibility decisions. Two
  engines with the same label are not the same engine; two engines
  with different labels are not different engines (the
  `<instanceId>` is what distinguishes them). When the label is
  omitted the engine records the absence as an empty string and
  emits a one-time info log warning that observability will be
  reduced — the warning is informational, never an error.
- **`--idle-timeout <seconds>`** overrides the 300-second default.
  The idle gate counts every connected client — pipe and stdio —
  the same way; see the MCP/stdio idle-timeout pitfall.
- **`--mcp-server <mode>`** is **the capability switch**, not a
  transport switch. Today's only accepted value is `with-stdio`,
  which registers `AddMcpServer().WithStdioServerTransport()...`
  on the engine's DI graph (the same wiring today's
  `AutoContext.Mcp.Server` does at `Program.Main`). The value
  shape leaves room for `--mcp-server with-http` if a future
  engine version needs MCP over Streamable HTTP without renaming
  the switch. Unknown values are hard-rejected; the engine never
  silently falls back to a different mode.
- **`--retention <duration>`** overrides the 1-day housekeeping
  retention window. Accepts a duration string `<n>{s|m|h|d}`
  (`30s`, `15m`, `12h`, `7d`); `0` disables retention entirely
  (sweep on every startup, no grace period). The value is
  validated for shape on argv parse and rejected if malformed;
  there is no host-wide minimum or maximum. The engine writes
  this value into its own `engine-metadata.json` row so peer
  engines doing the startup / shutdown sweep honour *this*
  engine's declared retention when classifying its leftover
  subtree as stale (see `### Housekeeping`). The same window
  governs rotated-log pruning within the engine's own
  per-instance subtree.
- **`--logging <verbosity>`** sets the in-process rotation
  thresholds for the engine's own `engine.log` and per-worker
  `worker-<workerId>.log` files. Accepted values are `normal`
  (default; rotate at 1,000 lines OR 5 MB) and `debug` (rotate at
  5,000 lines OR 25 MB). The switch does **not** change which
  records are emitted — log level filtering remains an in-process
  configuration concern, separate from rotation policy — only the
  size and line thresholds at which rotation fires. Unknown values
  are hard-rejected. See `### Housekeeping` > *Log rotation* for
  the rotation file-naming convention and the `Logs.*` RPC
  active-file contract.
- **`--version`** prints the engine's informational version (from
  `AssemblyInformationalVersionAttribute`) and exits. RID-independent.

Library-only knobs (not CLI flags). The `EngineOptions` callback
on `AddAutoContextEngine(...)` exposes additional knobs that
**deliberately do not surface on the command line** — corpus root
override, pipe-name override, and any future implementation-only
tuning. These are reachable only by in-process composition (tests,
embedders that call `AddAutoContextEngine` directly); the binary's
argv parser rejects them. The pipe-name override in particular
breaks P4's "one hash, reused everywhere" invariant, so keeping
it off the CLI surface is intentional — production hosts have no
way to set it.

### RPC surface (initial)

- `Engine.Hello` — handshake, returns
  `{ protocolVersion: <int>, engineVersion: <semver> }`. Issued by
  every client immediately after connect; mismatch on the integer
  refuses the engine.
- **`Engine.GetSharedMetadata`** — returns the current contents of
  the machine-wide engine-liveness registry
  (`…\autocontext\engine-metadata.json`) as an array of rows, one
  per live engine the registry knows about:

  ```
  Array<{
    workspaceHash:       string,   // sha256(normalisedWorkspacePath):0..16
    instanceId:          string,   // UUIDv4 the launcher minted
    instanceLabel:       string,   // freeform descriptor from --instance-label
    pid:                 number,   // OS process id of the engine
    processStartTimeUtc: string,   // ISO-8601, used with pid to defeat recycling
    engineVersion:       string,   // semver from AssemblyInformationalVersionAttribute
    startedAt:           string,   // ISO-8601 — when this row was written
    retention:           string    // duration string from --retention (e.g. "1d", "12h", "0")
  }>
  ```

  The engine reads the file when answering this RPC; it does not
  maintain an in-memory mirror, so the response always reflects
  whatever the on-disk registry currently records (including peer
  engines that started after this one). Callers must still
  pid-check each row before treating it as authoritative — a row
  whose `pid` no longer exists, or exists but whose
  `Process.StartTime` disagrees with `processStartTimeUtc` beyond
  the tolerance, is a stale crash leftover. The primary consumer is
  the engine's own housekeeping sweep (every live engine runs it on
  start and shutdown — see `### Housekeeping`); secondary consumers are observability surfaces — `autocontext ps`-style
  listings, tree-view "other live engines on this machine" badges,
  diagnostic dumps. The engine never RPCs peer engines; the registry
  file is the only cross-engine channel.
- **`Config.*`** — `Get`, `Subscribe`, `ToggleFile`, `ToggleRule`.
  The VS Code extension is the primary writer (UI toggles); other
  clients are typically subscribers. The engine is the only authority
  for what is enabled / disabled.
- **`Instructions.*`** — `List`, `Get(name)`, `GetAll`,
  `GetAlwaysAttached`, `GetRaw(name, opts?)`, `SearchContent(query, opts?)`,
  `Subscribe`. `List` returns identity rows; `Get` / `GetAll` /
  `GetAlwaysAttached` return **projected** bodies (disabled rules
  filtered out, `[INSTxxxx]` tags stripped, workspace override
  preferred over bundled); `SearchContent` searches the projected
  index; `GetRaw` returns the **source-faithful** bytes of the
  on-disk markdown file; `Subscribe` notifies on corpus reload.

  **`List(opts?)`** is the catalogue RPC — every other identity-shaped
  consumer (tree views, the `list_autocontext_instructions_files` LM
  tool, `search_autocontext_instructions_files_by_metadata`,
  `Discovery.*` index building) reads from it. Each entry carries:

  ```
  {
    key:            string,            // file basename, e.g. "dotnet-async-await"
    fileName:       string,            // "<key>.instructions.md"
    name:           string,            // frontmatter `name` ("<key> (vX.Y.Z)")
    version:        string,            // "X.Y.Z" parsed from name
    description:    string,            // frontmatter `description`
    applyTo?:       string,            // raw glob string (omitted if absent)
    hasChangelog:   boolean,           // sibling `<key>.CHANGELOG.md` exists
    contentHash:    string,            // "sha256:<hex>" over post-frontmatter body
    alwaysAttached: boolean,           // frontmatter `alwaysAttached: true`
    disabled:       boolean,           // engine-resolved against `.autocontext.json`'s `disabledInstructions`
    source:         "bundled"|"override",
    overridePath?:  string,            // workspace-relative when source="override"
    sections?:      Array<{ heading: string, anchor: string, parent?: string }>
  }
  ```

  Bodies are **never** in `List` — the tree-view bulk render would
  otherwise pull every body for nothing. `opts.includeSections` defaults
  to `true` (the LM-tool / discovery paths need them); tree-view callers
  pass `false` to drop the section payload. The section shape
  intentionally matches today's `instructions-files.metadata.json`
  generator output (`heading`, `anchor`, `parent?` — no `level`; the
  parent chain carries hierarchy).

  **`List` includes every bundled and override file** — disabled
  entries appear with `disabled: true` so the tree view can render
  the toggle UI, and the `alwaysAttached` flag lets the tree badge
  the meta-files distinctly. **`GetAll` filters out disabled files**
  unconditionally — it is the bulk-read path for tree-view rendering
  and CLI dumps; consumers that need disabled identity read `List`.

  **`Get(name)`** is a discriminated response, not a nullable one:

  ```
  type GetResponse =
    | { kind: "ok",        name, key, fileName, content, returnedSections, ... }
    | { kind: "disabled",  name, key }     // identity only — no description, no body
    | { kind: "not-found", name }          // name not in the corpus at all
  ```

  The `kind: "disabled"` envelope is **the** reason `Get` is not just
  a nullable string. LM tools (Copilot's
  `get_autocontext_instructions_file`) need to tell the model
  "this rule exists but the user has muted it" without leaking the
  body, the description, or the version — otherwise the model could
  quote the muted rule back from the description and route around
  the user's choice. `not-found` is a strictly distinct outcome
  (the name was never in the corpus, no user policy involved) and
  collapsing the two would make every "missing" response
  indistinguishable from "actively suppressed". Hooks consuming
  through `GetAlwaysAttached` and sub-agent materialisation paths
  treat `kind: "disabled"` as omission — the engine never returns
  a disabled identity envelope through `GetAlwaysAttached` because
  those consumption surfaces have no UI to render the muted state
  back to the user; only LM-facing surfaces with a model in the
  loop get the identity envelope.

  **`GetAlwaysAttached`** is the SessionStart / `PreCompact`
  consumer: it returns *only* the non-disabled files whose
  frontmatter declares `alwaysAttached: true`, in deterministic
  order. The flag is a per-file declarative signal in the source
  markdown's YAML frontmatter — today only
  `copilot.instructions.md` and `autocontext.instructions.md`
  carry it (they introduce AutoContext itself and must apply to
  every turn). Files with no `applyTo` but no `alwaysAttached`
  flag (`code-review`, `design-principles`, `git-commit`,
  `rest-api-design`) are domain-conditional, not universal, and
  surface only via `Discovery.RouteForPrompt`.

  **`SearchContent(query, opts?)`** is the engine-owned content
  search backing `search_autocontext_instructions_files_by_content`
  and any future CLI `autocontext instructions search <query>`. Today's
  TypeScript implementation reads every projected body to build a
  client-side trigram / inverted index on every cold start; moving
  the index into the engine (a) eliminates that startup cost,
  (b) keeps the index hot across queries, (c) tracks invalidation
  naturally via `Instructions.Subscribe` and the corpus reload
  generation counter, and (d) gives every other client — CLI,
  future JetBrains / Neovim shells — the same search without each
  re-implementing it. The response shape matches today's LM-tool
  output:

  ```
  Array<{
    name:        string, key: string, fileName: string, description: string,
    score:       number,
    excerpts:    Array<{ anchor: string, snippet: string, line?: number }>
  }>
  ```

  Disabled files are excluded from search by default (`opts.includeDisabled=false`);
  the LM tool flips this on only when explicitly asked to surface
  disabled guidance. The engine never indexes raw frontmatter or
  `[INSTxxxx]` tag noise — indexing runs on the same projected body
  that `Get` returns.

  **`GetRaw(name, opts?)`** returns the unmodified bytes of the
  on-disk markdown file — YAML frontmatter intact, `[INSTxxxx]`
  tags intact — with no disabled-state filter and no
  `alwaysAttached` filter. It exists as a separate method from
  `Get` because some callers need byte alignment with the source
  file the projected body cannot provide. The motivating case is
  the **rule enable/disable CodeLens**: the extension renders one
  lens per `[INSTxxxx]` tag at the tag's source-file line so the
  user can toggle individual rules, and the projected stream has
  those tags stripped — nothing for the lens to anchor to.
  "Open instruction source" commands, the corpus service's
  internal override-vs-bundled equality check, export tooling,
  and future raw-dump CLI verbs use it for similar reasons.
  Override resolution is under explicit caller control via
  `opts.source: "bundled" | "override" | "active"`:

  - `"active"` (default) — returns the override if one exists,
    else the bundled file. Matches the projection rule the rest
    of the surface uses; appropriate for callers that just want
    "the same content the engine would project from".
  - `"bundled"` — returns the bundled file even when an override
    exists. Used by the corpus service's internal override-vs-bundled
    equality check, and by UI callers whose user has opened the
    bundled file specifically.
  - `"override"` — returns the override or `kind: "not-found"`.
    Used by UI callers whose user has opened the override file.

  Callers whose byte offsets must align with a *specific* on-disk
  file (CodeLens lens positions, "open instruction source", future
  inline editors) must pass `"bundled"` or `"override"` explicitly
  — the source they pass must match the file the user actually has
  open. Silently retargeting a `bundled`-opened document to
  override bytes (or vice versa) shifts every byte offset and
  attaches toggles to the wrong rule.

  Response is a discriminated envelope mirroring `Get`:
  `{ kind: "ok", name, key, source, content }` /
  `{ kind: "not-found", name }`. There is no `kind: "disabled"`
  branch — disabled state is irrelevant to a source-file read.

  **`applyTo` matching: coarse engine-side filter, fine client-side
  matcher.** `applyTo` is consumed by three call sites — the
  `_by_metadata` LM tool's `applyTo` clause, the content-search
  `applyTo` post-filter, and the `Discovery.Route*` extension index.
  The engine handles the **coarse** layer ("could this file's
  `applyTo` ever match anything in this workspace?") and the
  client handles the **fine** layer ("does this file's `applyTo`
  match the user's specific glob right now?"). The split lives where
  it does because there is no portable cross-host equivalent of
  VS Code's `vscode.languages.match` — a hand-rolled .NET glob
  matcher would drift from editor semantics in edge cases
  (case sensitivity, `**` greediness against `.gitignore`-excluded
  paths, brace-expansion subtleties) — so fine matching stays on
  whichever host already owns a native matcher.

  Engine side (coarse, once at corpus-load):

  - **Parse, don't match.** A small lexical pass next to
    `InstructionsListBuilder` (Issue #4's build-time generator,
    repurposed as the build-side library that writes
    `Resources/instructions-files.json` and
    `Resources/instructions-files-metadata.json`; see
    `## Distribution > Resource manifests`) splits comma-separated `applyTo` strings, trims
    whitespace, brace-expands `**/*.{cs,fs,vb}` into individual
    globs, and extracts the **extension set** as a derived index.
    This is structural parsing, not glob algebra — it must round-trip
    (the recomposed glob list equals the original `applyTo` modulo
    whitespace) and it must not attempt to canonicalise globs,
    simplify `**` patterns, or otherwise reason about what a glob
    means. The parse result is **engine-internal state** consumed by
    the coarse filter and by `Discovery.RouteForPrompt`'s extension
    index; it is **not** published on the `List` envelope. The
    `List` row carries only the raw `applyTo` string, which clients
    hand verbatim to their host-native fine matchers
    (`vscode.languages.match` in VS Code,
    `Microsoft.Extensions.FileSystemGlobbing` in the CLI, `minimatch`
    in hooks). Comma-splitting and brace-expansion are trivially
    re-derivable from the raw string when a client ever needs them.
  - **Filter by workspace extension set.** `Instructions.List(opts)`
    accepts `opts.applyToWorkspaceFilter: boolean` (default `true`).
    When `true`, the engine drops every row whose internally-computed
    extension set is disjoint from `Workspace.Detect.extensions` — a
    workspace with no C# projects never sees C# files surface
    through `List`. The set intersection is cheap (small sets),
    reuses indices the engine already maintains for `Discovery.*`,
    and is the same trick `RouteForPrompt` uses today.
  - **Caller-supplied hint** (`opts.applyToHint?: string`) further
    narrows the candidate set on the engine side by extension only
    — `applyToHint: "**/*.cs"` keeps only rows whose internal
    extension set includes `.cs`. Hint matching is deliberately
    extension-only on the engine; full glob matching is the
    client's job.
  - **Opt-out** (`applyToWorkspaceFilter: false`) is the explicit
    bypass for the tree view's "show all instructions" mode and
    for export. Always-attached files are exempt from the
    workspace filter regardless — they apply to every workspace by
    definition.

  Client side (fine, per query):

  | Host | Fine matcher |
  |---|---|
  | VS Code extension | `vscode.workspace.findFiles` + `vscode.languages.match` (today's `InstructionsFilesLmToolsApplyToMatcher`, unchanged). Mirrors how `chatInstructions` decides which files attach. |
  | `autocontext` CLI | `Microsoft.Extensions.FileSystemGlobbing` against CWD with a 50-path cap (the same cap today's matcher uses for `findFiles`). |
  | Hook scripts (Claude Code, VS Code Copilot) | `minimatch` for the extension-index lookup the hook already performs today; no glob × glob intersection needed in the hook surface. |

  Both sides reading the raw `applyTo` string — the engine for
  internal coarse-filter derivation, the client for fine matching
  — guarantees the engine never excludes a row the client's fine
  matcher would have included: the coarse filter is a strict
  superset.
- **`Workspace.*`** — `Detect`, `Info`. Workspace-context detection,
  framework / language flags, override file inventory.

  **`Detect` return shape.** A flat record of named boolean flags
  (~60 today) plus two derived index fields the engine builds from
  the same source rules:

  ```
  {
    flags: {
      hasDotNet: boolean, hasCSharp: boolean, hasFSharp: boolean,
      hasVbNet: boolean, hasBlazor: boolean, hasRazor: boolean,
      hasXaml: boolean, hasWebForms: boolean,
      hasJavaScript: boolean, hasTypeScript: boolean,
      hasNodeJs: boolean, hasReact: boolean, hasAngular: boolean,
      hasVue: boolean, hasSvelte: boolean, hasNextJs: boolean,
      hasVitest: boolean, hasJest: boolean, hasJasmine: boolean,
      hasMocha: boolean, hasPlaywright: boolean, hasCypress: boolean,
      hasPython: boolean, hasJava: boolean, hasKotlin: boolean,
      hasScala: boolean, hasGroovy: boolean, hasC: boolean,
      hasCpp: boolean, hasRust: boolean, hasGo: boolean,
      hasSwift: boolean, hasRuby: boolean, hasPhp: boolean,
      hasLua: boolean, hasDart: boolean,
      hasHtml: boolean, hasCss: boolean, hasYaml: boolean,
      hasPowerShell: boolean, hasBash: boolean, hasBatch: boolean,
      hasDocker: boolean, hasUnity: boolean,
      hasAspNetCore: boolean, hasMaui: boolean, hasWpf: boolean,
      hasWinForms: boolean, hasEntityFrameworkCore: boolean,
      hasDapper: boolean, hasMediatR: boolean, hasSignalR: boolean,
      hasGrpc: boolean, hasGraphql: boolean,
      hasXunit: boolean, hasMsTest: boolean, hasNUnit: boolean,
      hasMongoDb: boolean, hasMySql: boolean, hasOracle: boolean,
      hasPostgres: boolean, hasSqlite: boolean, hasSqlServer: boolean,
      hasRedis: boolean
      // …authoritative list lives in the engine's detection rule
      // table, this enumeration is the public contract.
    },
    extensions: string[],          // union of all extensions the
                                   // active flags imply (e.g. hasCSharp → ".cs",
                                   // hasDotNet → ".csproj/.fsproj/.vbproj/.sln/.slnx")
    overrides: {                   // override file inventory
      paths: string[],             // workspace-relative paths under .github/instructions/
      names: string[]              // basenames (e.g. "lang-csharp")
    }
  }
  ```

  Each flag has a single deterministic source rule — either a glob
  set (e.g. `hasCSharp` is true iff at least one `**/*.csproj` exists)
  or a content pattern over a manifest file (e.g. `hasReact` is true
  iff `package.json` contains `"react":`). Activation rules add
  parent flags transitively: `hasNextJs` activates `hasReact`, which
  activates `hasNodeJs`. The full rule table is engine-internal but
  the schema above is the wire contract — new flags are additive,
  flag removal is a breaking change.

  The `extensions` field is the **single source of truth** the
  engine's coarse `applyTo` filter intersects against (see the
  `applyTo` matching subsection under `Instructions.*` above). It
  is derived from the same glob rules that drive flag detection,
  so any new file-rule flag automatically extends the extension
  set without a second declaration. Content-rule flags
  (`hasReact`, `hasEntityFrameworkCore`, …) contribute no
  extensions; they exist for instruction files whose `applyTo`
  would never differentiate them by file extension anyway.

  **`Info`** returns engine-process metadata (workspace path,
  engine version, generation counter, idle-timeout state) for
  diagnostics; it does not duplicate the `Detect` payload.
- **`McpTools.*`** — `List`, `Invoke`. `List` surfaces the engine's
  MCP tool catalogue (filtered by the same `disabledTools` /
  `disabledTasks` state) for hosts that want to introspect what the
  engine would advertise to an MCP client.

  **`Invoke(name, arguments)`** is the pipe-RPC counterpart of MCP's
  `tools/call`. Pipe-side consumers — the VS Code extension's
  MCP Tools tree-view "play" button, `autocontext mcp invoke <tool>
  --args <json>`, integration tests, and any future hook script that
  wants to re-run a tool outside the agent loop — invoke MCP tools
  through this RPC rather than spinning up a parallel MCP/stdio
  session against the same engine just to round-trip one
  `CallTool`. The MCP/stdio facade stays the canonical model-facing
  transport; `Invoke` is the canonical non-model transport. Both
  terminate at the same handler.

  Response is a discriminated union mirroring the `Instructions.Get`
  shape (Issue #5 rationale):

  ```
  type InvokeResponse =
    | { kind: "ok",           name, content: ContentBlock[], isError?: false }
    | { kind: "tool-error",   name, content: ContentBlock[], isError: true }
    | { kind: "schema-error", name, errors: Array<{ path, message }> }
    | { kind: "disabled",     name }   // identity only — no result, no schema leaked
    | { kind: "not-found",    name }
  ```

  `content` matches MCP's `CallToolResult.content` block array
  verbatim so pipe and stdio surfaces serialise byte-identically.
  `tool-error` distinguishes "the tool ran and reported failure"
  from "the engine refused to dispatch" (`disabled` / `not-found`),
  same correctness rationale as `Get`'s envelope split. The engine
  validates `arguments` against the tool's `inputSchema` before
  dispatch and emits `schema-error` on mismatch — the same
  validation the MCP/stdio path performs, sharing one validator to
  avoid drift. Cancellation piggy-backs on the pipe-RPC framing's
  per-request token (no separate `Cancel` RPC); the engine forwards
  it to the worker, which honours it through the existing `IMcpTask`
  cancellation parameter. Worker dispatch is engine-internal:
  callers never see workers, never spawn them, and never know which
  worker handles which tool — the mapping lives in the embedded
  `mcp-tools-registry.json` the engine already owns (see the
  resource-manifest layout under `## Distribution` below).

  Out of scope for today, design constraint only: a future
  `McpTools.InvokeStream` sibling will mirror MCP's
  `notifications/progress`; `Invoke`'s shape must not preclude
  adding it. Schema exposure on the pipe is also out of scope here
  — it lands together with the meta-discovery forward-note below
  (`McpTools.GetDescription` / `SearchByMetadata` / `SearchByContent`).

  Forward-note (out of scope here, design constraint only): the same
  shape `Instructions.*` uses for instruction-file discovery — list /
  search-by-metadata / search-by-content / get — should eventually
  exist for the MCP tools themselves, as `McpTools.SearchByMetadata`
  / `McpTools.SearchByContent` / `McpTools.GetDescription` backing
  `mcp_tools_list` / `mcp_tools_search_*` / `mcp_tools_get` MCP tools
  (and their LM-tool shims). Categories and descriptions are already
  collected in the embedded registry; an `McpToolsContentIndex`
  mirror of `InstructionsContentIndex` closes the symmetry. Today's
  `McpTools.List` envelope must therefore be designed so adding those
  siblings later is additive — leave room for `description`,
  `categories`, optional section-like metadata, and a stable `key`
  field per tool, even if the only consumer today is
  `Discovery.RouteForPrompt`.
- **`Discovery.*`** — `RouteForPrompt(prompt)`,
  `RouteForTool(toolName)`. `RouteForPrompt` powers the
  `UserPromptSubmit` hook's routing preamble and any future
  CLI / host that wants the same "what's relevant for this prompt"
  signal; `RouteForTool` powers the `PreToolUse` hook by mapping a
  tool identity to the instruction files whose `applyTo` would
  surface for that tool's domain (e.g. `analyze_csharp_code` →
  the C# coding-standards files). The engine builds two indices
  from already-owned state — *category → MCP tools* (inverted from
  each tool's `categories` array in `McpTools.List`) and *file
  extension → instruction files* (extracted from each instruction
  file's `applyTo` glob via `Instructions.List`) — and runs the
  same word-boundary literal scan (categories) plus
  `\.[A-Za-z][A-Za-z0-9]{0,12}` regex scan (extensions) the
  current `.cjs` does today. The `RouteForPrompt` response is
  `{ matchedCategories[], matchedExtensions[], tools[], instructions[] }`,
  filtered by the same disabled-state the engine applies elsewhere;
  `RouteForTool` returns the `instructions[]` slice keyed off the
  tool's declared category / extension affinity. Routing lives
  engine-side because the indices are already engine state,
  invalidation tracks `Instructions.Subscribe` /
  `Config.Subscribe` automatically, and the same RPC family can
  back an `autocontext route "<prompt>"` debug helper without
  duplicating the scan logic in TypeScript.

  Out of scope for `Discovery.*`: **host-specific tool registration**.
  VS Code Copilot's `vscode.lm.registerTool` LM tools are a registration
  surface specific to the VS Code extension host — Claude Code has no
  equivalent — and the engine does not own that registration. What the
  engine *does* back is every LM-tool's underlying handler (see the
  LM-tool surface section below); the engine simply doesn't care which
  hosts choose to register an LM-tool wrapper around it.

### LM-tool surface (host-specific registration, MCP-backed handlers)

Post-engine, the four instruction-discovery tools the VS Code
extension contributes today
(`list_autocontext_instructions_files`,
`search_autocontext_instructions_files_by_metadata`,
`search_autocontext_instructions_files_by_content`,
`get_autocontext_instructions_file`) are no longer extension-native.
The engine owns the only implementation, and that implementation is
reachable through two parallel surfaces — pipe RPC for in-host
clients, MCP/stdio for any MCP-aware host:

```
                      handler (engine-owned, single impl)
                                    ▲
                ┌───────────────────┴───────────────────┐
                │                                       │
        Engine pipe RPC                          Engine MCP/stdio
        (Instructions.*)                         (instructions_*)
                │                                       │
        in-host clients:                         every MCP host:
        VS Code extension UI,                    Claude Code,
        autocontext CLI subcommands,                 Cursor, Inspector,
        hook scripts                             autocontext CLI MCP mode,
                                                 VS Code LM-tool shims
```

- **Engine pipe RPC** — `Instructions.List` / `SearchContent` / `Get`
  / `GetAlwaysAttached`, as specified above. Lowest latency, richest
  typed surface, consumed by clients running in the same host
  process tree as the engine pipe.
- **Engine MCP/stdio facade** — exposes `instructions_list`,
  `instructions_search_metadata`, `instructions_search_content`,
  `instructions_get` as MCP tools, **always registered
  unconditionally**. Each MCP-tool handler is a paper-thin adapter:
  deserialise MCP input → call the corresponding engine RPC handler
  in-process → serialise the result. Both surfaces share one set of
  service implementations (`InstructionsCorpusService`,
  `InstructionsContentIndex`, `InstructionsListBuilder`); the MCP
  facade is a transport, not a re-implementation.
- **VS Code LM-tool shims** — the extension keeps registering the
  four `vscode.lm.registerTool` entries it ships today, but each
  shim's `invoke` body forwards to the corresponding MCP tool on the
  bundled engine's MCP/stdio surface. The shim never dials the
  engine pipe directly: routing it through the MCP facade guarantees
  byte-identical output between the LM-tool and MCP paths and means
  any change to the MCP tool's schema is automatically reflected in
  the LM tool.

**Double exposure is intentional, no suppression flag is needed.**
Inside VS Code Copilot the model sees both `#list_autocontext_instructions_files`
(first-class LM tool, never deferred, `#`-mentionable) and
`mcp_autocontext_instructions_list` (deferred MCP tool, reachable via
`tool_search`). Either path terminates at the same engine handler,
so the outcomes are identical. The LM tool exists solely to **escape
the deferred-tool discoverability tax** by promoting the discovery
surface into the always-available LM-tool list; the parallel MCP
variant is harmless because there is no semantic divergence between
the two paths to suppress. This is a deliberate inversion of the
original suppression-flag motivation, which assumed two surfaces
implied two possible behaviours.

Naming convention split (kept intentionally — three labels for one
handler):

| Surface | Tool names | Why this shape |
|---|---|---|
| Engine pipe RPC | `Instructions.List`, `Instructions.SearchContent`, `Instructions.Get`, `Instructions.GetAlwaysAttached` | Dotted, namespaced — matches the rest of the engine RPC vocabulary (`Config.*`, `Workspace.*`, `Discovery.*`, `McpTools.*`, `Agent.*`). |
| Engine MCP/stdio | `instructions_list`, `instructions_search_metadata`, `instructions_search_content`, `instructions_get` | snake_case prefix-grouped — standard MCP-tool convention, consistent with the analyzer tools the engine already exposes (`analyze_csharp_code`, `read_editorconfig`, …). |
| VS Code LM tools | `list_autocontext_instructions_files`, `search_autocontext_instructions_files_by_metadata`, `search_autocontext_instructions_files_by_content`, `get_autocontext_instructions_file` | Verb-first, fully self-describing — the LM-tool name is what the model sees in its tool list, so it reads like documentation. |

Breaking the LM-tool names would force migration of every
`copilot.instructions.md` reference and every existing user's mental
model of which tool to ask for; keeping all three name shapes is the
small blemish that buys consistency on each surface.
- **`Engine.Lifecycle`** — `Subscribe`. Streams engine-lifecycle
  events to every connected client: `started` (sent immediately on
  subscribe so clients always know the current generation),
  `reloading` (config or corpus reload in progress),
  `reloaded` (post-reload, with a generation counter so clients
  can invalidate caches), `shuttingDown` (idle timeout fired or
  signal received, fixed grace period before the pipe closes).
  This is the authoritative channel for engine-owned lifecycle;
  clients respond by invalidating caches, refreshing UI, or
  flushing host-local materialisations. Contrast with
  `Config.Subscribe` and `Instructions.Subscribe`, which stream
  *content* deltas; `Engine.Lifecycle` streams *process*
  transitions.
- **`Agent.*`** — `SubagentStarted`, `SubagentStopped`,
  `Compacted`, `ToolUsed`, `TurnEnded` (all fire-and-forget
  notifications from a hook script to the engine), plus
  `Events.Subscribe` (server-streaming). The hook scripts are
  the only sensor the engine has on agent-loop transitions; these
  RPCs turn that sensor's readings into engine-broadcast signals
  every other client can observe. The VS Code extension
  subscribes to `Agent.Events` to render "active sub-agents"
  panels, "compaction in progress" status indicators, and
  per-session tool-usage hints in tree views — observability the
  chat surface alone does not provide. Notifications are
  best-effort: lost events are tolerable (the engine never makes
  *correctness* decisions from agent events; only UX
  enhancements). See the agent-plugin section above for the
  per-hook design rationale.
- **`Engine.WriteLog`** — fire-and-forget notification (no response,
  no ack) used by **workers** to forward `ILogger<T>` records into
  the engine's log files. The handler deserialises one record, routes
  it by `category` prefix (records matching `worker.<workerId>` /
  `worker.<workerId>.<Type>` are appended to
  `…\<workspaceHash>#<instanceId>\logs\worker-<workerId>.log`,
  everything else is appended to
  `…\<workspaceHash>#<instanceId>\logs\engine.log`), and fans the
  record out to every subscriber on the `logs` pipe and to any active
  `Logs.Tail*` RPC subscriber. Routing is by prefix alone; workers
  never specify a destination and the engine never asks. Both files
  are engine-owned per P5 — the engine is the sole writer of every
  file under `…\logs\`. Under normal operation workers never open
  their own log file under the per-instance subtree, never dial the
  `logs` pipe as a producer, and never write to stdout/stderr — the
  engine spawns workers detached with redirected/null stdio (same
  spawner discipline the engine itself is launched under, see the
  *Engine termination signal* pitfall); the engine supervises each
  worker via `Process.Start` and emits each captured stderr line
  as an engine-side log record under category
  `worker.<workerId>.engine.stderr` — which the prefix router lands
  in that worker's `worker-<workerId>.log` alongside the worker's
  own in-band records, so
  the rare write that bypasses the worker's logging provider is still
  observable. When the engine is briefly unreachable (engine
  mid-shutdown, RPC pipe unbound during a startup race, worker still
  inside cold-start), the worker buffers records in a bounded
  in-memory ring and replays on reconnect — see the *Worker–engine
  connectivity* pitfall for the loss semantics. Record shape:

  ```
  {
    timestamp:   string,           // ISO-8601 UTC, set by the worker at log time
    category:    string,           // dotted, prefix-groupable; see *Log categories*
    level:       "trace" | "debug" | "information" | "warning" | "error" | "critical",
    eventId?:    { id: number, name?: string },
    message:     string,           // formatted message body
    properties?: object,           // structured KV state (Microsoft.Extensions.Logging scope+state)
    exception?:  { type: string, message: string, stackTrace: string, inner?: object }
  }
  ```

  The shape is the canonical wire form for **every** record the
  engine emits on the `logs` pipe, whether the record originated
  from an engine-internal `ILogger<T>` call (where the engine fills
  the envelope directly) or from a worker's `Engine.WriteLog`
  notification (where the worker's `ILoggerProvider` fills it and
  ships it). Both paths terminate at the same sink (P1 — one
  handler, transport-agnostic). The notification is **best-effort**:
  the engine bounds its in-memory ingest buffer and drops oldest
  records on overflow, emitting a single synthetic record
  (`category: "engine.logging"`, `level: "warning"`,
  `message: "dropped N worker log records"`) the next time the
  buffer drains; workers never block on `WriteLog` and never await
  delivery. See the *Log pipeline backpressure* pitfall for the
  failure-mode discipline.
- **`Logs.*`** — `GetEngine(opts?)`, `TailEngine(opts?)`,
  `GetWorker(workerId, opts?)`, `TailWorker(workerId, opts?)`. The
  structured pipe-RPC surface for the per-instance log files the
  engine owns under `…\<workspaceHash>#<instanceId>\logs\`.
  `engine.log` and `worker-<workerId>.log` are written by the engine —
  directly for engine-emitted records, and via `Engine.WriteLog` for
  worker-emitted records routed to the right per-worker file by the
  record's `category` prefix (`worker.<workerId>.*` →
  `worker-<workerId>.log`; everything else → `engine.log`). One file
  per spawned worker; the file is created lazily on the worker's
  first record and rotated by the engine's in-process rotation
  logic (`--logging` thresholds), so the active file tracks the
  current rotation window and rotated history sits beside it under
  retention (see `### Housekeeping`).

  `Get*` and `Tail*` return discriminated envelopes (P2):

  ```
  type GetResponse =
    | { kind: "ok",        records: LogRecord[], truncated: boolean }
    | { kind: "not-found", workerId: string }   // GetWorker / TailWorker only

  type TailFrame =
    | { kind: "ok",        record: LogRecord }                 // streamed per record
    | { kind: "not-found", workerId: string }                  // terminal, GetWorker / TailWorker only
    | { kind: "evicted",   reason: "slow-subscriber" }         // terminal, see backpressure rule below
  ```

  `not-found` distinguishes "this `workerId` is not a worker the
  current engine has ever spawned" from `kind: "ok"` with an empty
  `records` array ("a real worker that simply hasn't logged yet")
  — a CLI subcommand or tree-view tooltip needs to tell the two
  apart. `GetEngine` / `TailEngine` never return `not-found` (the
  engine's own log file always exists for the current process).

  `opts.lastN` caps from the tail (default unlimited, but practical
  clients pass a bound); `opts.since` filters by timestamp;
  `truncated: true` signals the file rolled past the requested
  range. `Tail*` server-streams new records as the engine appends
  them, replaying from `opts.since` if given (default: stream from
  connect-time onward). Same `LogRecord` envelope as the `logs` pipe
  and `Engine.WriteLog`:
  `{ timestamp, category, level, eventId?, message, properties?, exception? }`.

  `Get*` and `Tail*` exist as separate verbs so a tree-view
  "show last 200 lines" render does not have to deal with stream
  lifetime, and `Tail*` exists so the live-follow path does not have
  to poll. Two RPCs per file (engine, worker) keep the per-worker
  routing the engine already does on ingest visible at the RPC
  surface — the alternative ("one `Logs.Tail(filter)` with a
  category-prefix predicate") would push that routing into every
  client. Workers themselves never call `Logs.*`; the surface is
  read-only and engine-served.

  **Backpressure on `Tail*`.** Each `Logs.Tail*` subscriber inherits
  the same non-blocking fan-out discipline the `logs` pipe uses
  (see the *Log pipeline backpressure* pitfall): a per-subscriber
  bounded send buffer, and a subscriber that cannot drain in time
  is disconnected (its stream closed with a terminal
  `{ kind: "evicted", reason: "slow-subscriber" }` frame) rather
  than allowed to back-pressure the file sink, the ingest loop, or
  any other subscriber. The "losing a subscriber must never affect
  engine progress" contract applies regardless of whether the
  subscriber arrived via the raw `logs` pipe or via `Tail*` RPC.

  Relationship to the `logs` pipe: the `logs` pipe is the **passive,
  no-handshake, raw-NDJSON** debugging surface — `Get-Content -Wait`
  and `nc` work without a client library, and it streams every
  record the engine sees (engine and worker, distinguished by
  `category`). `Logs.*` is the **structured, handshake-required,
  per-file** surface — clients that already speak `rpc` (tree views,
  CLI subcommands, future hosts) use it to ask for one specific file
  with a request-shaped API and a bounded snapshot mode. Two
  surfaces, different audiences, identical record schema.
- **Future:** `Diagnostics.Run`, host-specific notification
  channels.

### Log categories

Every record on the `logs` pipe carries a `category` string that
identifies its origin. Categories are dotted, prefix-groupable
literals matching `Microsoft.Extensions.Logging` category
conventions — subscribers filter by prefix-match, not enum-equals,
so new producers can introduce new prefixes without bumping the
protocol. The taxonomy is **convention, not closed enum**: the wire
field stays `string`; documented prefixes are the recommendation
that keeps logs grep-friendly across hosts.

Current prefixes:

| Prefix | Producer | Typical sub-categories |
|---|---|---|
| `engine.rpc` | engine | per-RPC-handler `ILogger<T>` categories (e.g. `engine.rpc.Instructions.Get`) |
| `engine.events` | engine | `Engine.Lifecycle` broadcaster, `Agent.Events` fan-out |
| `engine.health` | engine | health-pipe accept/handshake trace |
| `engine.lifecycle` | engine | startup, shutdown, idle-timeout transitions |
| `engine.startup` | engine | argv parse, pipe bind, manifest load — anything before accepting connections |
| `engine.logging` | engine | the log pipeline's own diagnostics (buffer overflow drops, slow subscriber evictions) |
| `worker.<workerId>.engine.stderr` | engine | captured worker stderr forwarded by the engine's process-supervision channel (the rare worker write that bypasses the logging provider); routes into `worker-<workerId>.log` by the same `worker.<workerId>.*` prefix rule that handles in-band worker records |
| `worker.<workerId>` | worker | one prefix per worker (`worker.dotnet`, `worker.workspace`, `worker.web`); `<workerId>` matches the worker's `id` in `workers.json` |
| `worker.<workerId>.<Type>` | worker | per-type sub-categories under a worker (e.g. `worker.dotnet.RoslynAnalyzer`); free-form below the worker prefix |

Worker-side seam (composition contract): worker hosts register
`AddEngineLoggerProvider()` from `AutoContext.Worker.Shared` during
startup. That provider serialises every `ILogger<T>` record into
the `Engine.WriteLog` notification with the worker's `id` baked
into the `category` prefix; the worker codebase itself never sees
the transport choice. Workers therefore use `ILogger<T>` exactly
as any other .NET service does, and the engine remains the single
owner of the on-disk log file and the wire log stream.

### Naming

- **`<name>`** in `Instructions.{Get,GetRaw,Subscribe}` is the bundled
  file's stem (filename without `.instructions.md`), case-sensitive
  on POSIX, case-preserving on Windows. Override resolution looks for
  `<workspace>/.github/instructions/<name>.instructions.md` and
  prefers the override over the bundled source byte-for-byte.
- **`<workspaceHash>`** is `sha256(normalisedWorkspacePath):0..16` —
  the same prefix used in the pipe name. It identifies the
  *workspace*; on its own it is not sufficient to address any
  on-disk artefact, because every artefact is scoped to a
  (workspace, launcher-instance) pair.
- **`<instanceId>`** is the launcher-minted UUIDv4 passed on
  `--instance-id`. It appears as the `#<instanceId>` suffix of
  every pipe name (one UUID, four pipes sharing it within a
  launcher) **and** as a path segment in every per-instance
  on-disk artefact: engine logs and client caches all live under
  `…\autocontext\<workspaceHash>#<instanceId>\` (Windows; POSIX
  equivalent under the OS user-cache root). Two launchers on the
  same workspace therefore get disjoint on-disk subtrees — they
  cannot interleave each other's log lines, and a hook running
  under one launcher cannot read or corrupt cache files written
  by a hook under the other.

## Authority model: engine owns, clients cache

The engine is the single owner of every piece of AutoContext state
for a workspace — config, instructions corpus, projection,
workspace-context detection, MCP tool catalogue, worker lifecycle.
Clients (VS Code extension, Anthropic plugin, `autocontext` CLI) are
**caches with UI**, never authorities. The contract is one-way:

- **Reads go through the engine.** Even if a client has a local
  cache for a host-specific reason (an Anthropic sub-agent file
  path, a future tool that demands a `Uri`), the cache is
  derived from an engine RPC, not from disk inspection or
  re-projection.
- **Writes go through the engine.** `Config.ToggleFile` /
  `Config.ToggleRule` are RPCs; clients never edit
  `.autocontext.json` directly. The engine validates,
  persists, and broadcasts the change.
- **Cache invalidation is engine-driven.** Clients learn that
  their caches are stale by subscribing to engine events, not by
  polling, watching files, or rebuilding projection on their
  side. The relevant event channels are `Config.Subscribe`,
  `Instructions.Subscribe`, and `Engine.Lifecycle.Subscribe`.
  Together they cover content changes and process transitions;
  there is no fourth channel a client needs to invent.
- **Lifecycle events are first-class.** A client must subscribe
  to `Engine.Lifecycle` early (right after `Engine.Hello`) and
  treat its events as authoritative process-state transitions.
  In particular:
  - On `started` / generation change, the client invalidates
    every host-local cache (the engine may have hot-reloaded
    config or restarted; generation counters distinguish the
    two).
  - On `reloading`, the client may show a transient "refreshing"
    UI affordance but **must not** issue redundant content RPCs
    — the matching `reloaded` event will arrive with the new
    generation.
  - On `shuttingDown`, the client stops accepting user actions
    that would issue writes (toggles, override exports), drains
    any in-flight reads, and treats subsequent connect failures
    as "engine restarting, retry under the cold-start protocol"
    rather than "engine crashed".

Client cache cleanup is a client concern. The engine emits
lifecycle events and content-change subscriptions; what a client
materialises to disk and how it cleans up after itself is the
client's contract with its host (VS Code's extension storage,
Anthropic's session lifecycle, the OS user-cache directory). The
engine itself caches *only* in-memory state that invalidates on
internal events — no engine-owned on-disk artefact ever needs an
external cleanup actor, by design.

## Projection ownership

The engine is the **only** writer of projected instruction state.
**All projection happens in-memory**, on every read, from the
workspace's `.autocontext.json` plus the raw corpus — there is no
on-disk projection artefact at all. `Instructions.Get` returns the
projected body as a string over the pipe. This eliminates:

- The `<extensionPath>/instructions/.generated/` shared folder.
- Per-workspace `.workspaces/` projection output directories and the
  metadata generator that wrote them.
- The cross-window / cross-host lock-file dance.
- The read-only-mount problem on Claude plugin installs.

The only on-disk artefacts under `instructions/` are the source
markdown files (`*.instructions.md`) and any user overrides at
`<workspace>/.github/instructions/`.

Hosts that need a file path get one of two patterns:

- **VS Code extension:** does not need a file path. Instructions
  reach the chat surface through the agent-plugin SessionStart
  hook (see below), not through any VS Code `chatInstructions`
  declaration — and that hook fires whether the host is Claude
  Code or VS Code Copilot. The extension is a pure RPC consumer
  — tree views, decorations, hovers, and previews all consume
  projected bodies as strings from `Instructions.Get` /
  `Instructions.GetAll`. No projection cache, no static-path
  mirror, no on-disk artefact under `<extensionPath>`. Commands
  that open an instruction *source* in the editor open the
  bundled file at `<extensionPath>/engine/Instructions/...`
  or the workspace override at
  `<workspace>/.github/instructions/...` — neither is a
  projected body, so neither requires a cache.
- **Agent-plugin SessionStart hook:** calls `Instructions.GetAlwaysAttached`
  and returns the bodies inline as `additionalContext`. The set is
  small by design (2 files today: `copilot` and `autocontext`) and
  curated by the `alwaysAttached: true` frontmatter flag in the
  source markdown — not by `applyTo` absence. No file ever gets
  written under `${CLAUDE_PLUGIN_ROOT}`. Sub-agents that need
  file paths materialise them under the per-instance cache root
  (`%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\cache\`
  on Windows,
  `$XDG_CACHE_HOME/autocontext/<workspaceHash>#<instanceId>/cache/`
  or `~/.cache/autocontext/<workspaceHash>#<instanceId>/cache/`
  on POSIX). The hook owns this cache: SessionStart writes,
  SessionEnd cleans, and the engine never reads or writes those
  paths.

General rule for any future client cache: write under the
per-instance cache root
(`%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\cache\<client>\`
on Windows,
`$XDG_CACHE_HOME/autocontext/<workspaceHash>#<instanceId>/cache/<client>/`
or `~/.cache/autocontext/<workspaceHash>#<instanceId>/cache/<client>/`
on POSIX), never under the host's install directory
(`<extensionPath>`, `${CLAUDE_PLUGIN_ROOT}`). Install directories
are read-only on managed installs and get wiped on host upgrade;
the OS cache root is writable, survives host upgrades, and gives
every client one consistent place to find and clean its
launcher-instance-scoped artefacts. The per-instance segment
(`<workspaceHash>#<instanceId>`) is what isolates one launcher's
on-disk state from another's — two VS Code windows open on the
same workspace get disjoint cache subtrees, so a sub-agent hook
in one window cannot stomp materialised files belonging to a
sub-agent in the other. Engine logs live as a sibling `logs\`
directory under the same per-instance root (see P4 / P5 below),
so a postmortem reader who has the launcher's UUID can find every
artefact that engine produced in one place.

## Sharing principle (overarching)

**The engine is .NET; hosts are clients.** All projection, config,
and instruction-corpus logic lives in **one** place — the engine
binary, sourced from `AutoContext.Engine/` — written in C#. Every
host (VS Code extension, Anthropic plugin, `autocontext` CLI, future
JetBrains / Neovim shells) is a *client* of the engine. Sharing
happens at the **wire-protocol** level (named-pipe RPC), not at the
source-code level.

Consequences:

- **One implementation, one home.** `AutoContextConfigStore`,
  `InstructionsFileBodyProjector`, `InstructionsCorpusReader`,
  `InstructionsCorpusService`, the engine's hosted services, and
  every RPC handler all live in `AutoContext.Engine/`. The engine
  binary is the only producer.
- **The VS Code extension keeps no co-projector.** Once the engine
  ships, the extension's TS-side `AutoContextConfigManager`,
  `InstructionsFilesManager`, `InstructionsFileContentProjector`,
  `LogServer`, `HealthMonitorServer`, `WorkerControlServer`,
  `AutoContextConfigServer`, and any in-process projection code are
  *deleted*. The extension's remaining responsibility is wiring
  `AutoctxClient` (TS) to its tree views, codelens providers, and
  decoration providers. No on-disk projection cache lives in the
  extension — the agent-plugin hooks handle chat-side instruction
  delivery (under whichever hook host is running, including
  VS Code Copilot in the same window).
- **`AutoctxClient` is the only shared TS class.** A thin pipe-RPC
  client living in `Framework.Web/src/cli/`. Used by the VS Code
  extension and by the agent-plugin `.cjs` hook scripts (under
  whichever hook host runs them). Speaks the same wire protocol
  the engine serves.
- **No invented cross-host seams.** This is *not* a ban on .NET DI.
  It is a ban on inventing portability interfaces (`IFileSystem`,
  `IWorkspace`, a custom `IHostEnvironment`-shaped wrapper) just to
  pretend the C# engine and the TS extension share code. They do
  not share code; they share a wire protocol. Inside the engine, use
  `Microsoft.Extensions.Hosting.IHostEnvironment`, `ILogger<T>`,
  `IOptions<T>`, and `IConfiguration` exactly as the rest of the
  .NET solution does. New interfaces only appear when a *second
  concrete* implementation is being added now — not hypothetically
  later.
- **Duplication is the lesser evil vs. abstraction.** A few lines
  repeated between the C# engine and a hypothetical second .NET
  host are fine. An interface invented to deduplicate them is not.
- **Shells stay thin.** `AutoContext.CommandLine` and `AutoContext.VsCode`
  contain almost nothing but: arg / activation parsing, host-specific
  surfaces (vscode UI, CLI argv), the `AutoctxClient` plumbing, and
  the run / teardown loop. Logic that is not host-specific belongs
  in the engine.

## Design principles (cross-cutting)

Each per-feature section above ends up restating the same handful of
invariants. Hoisting them here so the implementation has one place to
check against, and so future capabilities inherit the rules instead
of re-deriving them.

These are **architectural guidelines, not implementation
specifications** — they constrain *what shapes are allowed*, not
*which classes hold which methods*. Specific service names,
namespaces, file layouts, and DI wiring are the implementation's
choice; conformance with the principles below is non-negotiable.

### P1. One handler per capability; transports are marshalling shims

Every capability (instructions discovery, MCP-tool invocation, config
read/write, workspace detection, prompt routing, agent-event
broadcast) has **one** engine-side service implementation. Pipe RPC,
MCP/stdio, and host-specific LM-tool registrations are
deserialise → call-the-one-handler → serialise shims with no business
logic of their own.

- A new transport never adds capability; a removed transport never
  removes it.
- A field that can't be expressed on the engine RPC envelope can't be
  expressed on any transport.
- Validation rule: a recorded response on transport A diffs cleanly
  against transport B for the same input. The pipe `McpTools.Invoke`
  response and the MCP/stdio `tools/call` response must produce zero
  diff for `content`; the LM-tool `get_autocontext_instructions_file`
  result and the MCP `instructions_get` result must produce zero
  diff for the projected body.
- The named instances of this principle in this doc — instruction-discovery
  shims, `McpTools.Invoke` shim — are illustrations, not exhaustions.

### P2. Discriminated envelopes for state-bearing reads

Any RPC whose answer can be "the thing exists but the user has muted
it" returns a discriminated envelope (`ok` / `disabled` / `not-found`
/ `*-error`), never a nullable. `Instructions.Get` and `McpTools.Invoke`
are the seed cases. Every future state-bearing read inherits the
shape:

- `not-found` and `disabled` are strictly distinct outcomes and must
  not be collapsed by any client or transport.
- LM-facing surfaces (model in the loop) surface `disabled`
  verbatim so the model can tell the user what's muted; consumption
  surfaces with no UI (`GetAlwaysAttached`, sub-agent materialisation,
  `additionalContext` injection) treat both outcomes as omission. The
  envelope shape stays the same; the *consumption rule* differs by
  surface.
- Identity-only envelopes (`disabled`, `not-found`) leak nothing
  beyond the queried name — no description, no version, no schema —
  so a model cannot route around the user's mute by reading the
  metadata.

### P3. Wire shape ≠ engine-internal shape (split the manifests)

When a build-generated artefact would otherwise carry both the
public envelope **and** engine-internal indices, split it into two
files: one matches the wire envelope verbatim
(`instructions-files.json`, `mcp-tools.json`); the sibling carries
internal-only state (`instructions-files-metadata.json` —
section-anchor maps, parsed `applyTo` extension sets, content-index
seed).

- The wire file must round-trip against the public RPC envelope; a
  unit test asserts equality.
- The internal file may evolve freely without bumping the protocol
  version.
- Don't publish derived structure on the wire just because the engine
  derived it. Every published field is a field future engine versions
  must keep producing.
- The parsed `applyTo` extension set is the canonical example: the
  engine needs it for its coarse filter and `Discovery.RouteForPrompt`
  index, but it stays in `instructions-files-metadata.json` and
  never appears on `Instructions.List`. Clients re-derive it from the
  raw `applyTo` string trivially when (if) they need it.

### P4. Workspace identity is one hash; engine identity adds one UUID

`<workspaceHash> = sha256(normalisedWorkspacePath):0..16` is **the**
workspace identifier. Path normalisation (resolve symlinks,
lowercase on Windows) happens once; hashing happens once. Engine
identity adds **one** launcher dimension on top — `<instanceId>`,
a UUIDv4 minted by the launcher and passed verbatim on
`--instance-id`. Pipe names carry both; every other workspace-scoped
artefact reuses the hash alone:

| Artefact | Path |
|---|---|
| Pipe names (four, one per kind, per launcher instance) | `autocontext-engine:rpc@<workspaceHash>#<instanceId>`, `autocontext-engine:events@<workspaceHash>#<instanceId>`, `autocontext-engine:health@<workspaceHash>#<instanceId>`, `autocontext-engine:logs@<workspaceHash>#<instanceId>` |
| Per-instance engine subtree (logs + future engine-owned artefacts) | `%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\` (Windows) / `$XDG_CACHE_HOME/autocontext/<workspaceHash>#<instanceId>/` or `~/.cache/autocontext/<workspaceHash>#<instanceId>/` (POSIX) |
| Engine log files | `…\<workspaceHash>#<instanceId>\logs\engine.log` (and future `errors.log`), under the per-instance subtree above |
| Per-worker log files (one per spawned worker; engine-owned, routed by `category` prefix) | `…\<workspaceHash>#<instanceId>\logs\worker-<workerId>.log` |
| Client cache root | `…\<workspaceHash>#<instanceId>\cache\<client>\`, under the same per-instance subtree |
| Shared engine-liveness registry (one file, shared by every live engine on the machine) | `%LOCALAPPDATA%\autocontext\engine-metadata.json` (Windows) / `$XDG_CACHE_HOME/autocontext/engine-metadata.json` or `~/.cache/autocontext/engine-metadata.json` (POSIX) |

A new on-disk artefact must reuse the `<workspaceHash>#<instanceId>`
compound segment; never invent a parallel identifier and never
flatten the compound back into a workspace-only path. The same
workspace from different launchers hashes to one workspace identity
but resolves to different engines (different `<instanceId>` in the
pipe name and in the on-disk subtree); different workspaces hash to
different identities regardless of launcher. Symlink and case
normalisation exist precisely to collapse the unintentional
multi-engine cases that arise from path-shape differences alone —
the launcher dimension is additive on top, and is intentionally not
collapsed. Per-instance scoping for both logs and client caches is
the price of isolation: two launchers on the same workspace must
not interleave their log lines (a postmortem reader needs to
identify which launcher crashed, not assemble a merged history) and
must not share a cache root (a hook in one launcher would otherwise
be able to read or corrupt files a hook in the other wrote). The
cost is that postmortem and disk-usage tools must enumerate
per-instance subdirectories under `…\autocontext\` rather than
looking at one flat workspace-keyed file.

### P5. On-disk path ownership is explicit and exclusive

Every on-disk path AutoContext touches has exactly one owner:

| Path | Owner | Read | Write |
|---|---|---|---|
| `<workspace>/.autocontext.json` | engine | engine | engine |
| `<workspace>/.github/instructions/<name>.instructions.md` | user | engine | user |
| `<host-bundle>/engine/...` (`<vsix>/`, `<plugin-root>/`, GitHub-release tarball) | build | engine reads bundled side-cars at startup | nobody at runtime |
| `%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\logs\engine.log` (and future `errors.log`; POSIX equivalent) | engine | engine, postmortem readers, `Logs.GetEngine` / `Logs.TailEngine` callers | engine |
| `%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\logs\worker-<workerId>.log` (POSIX equivalent) | engine | engine, postmortem readers, `Logs.GetWorker` / `Logs.TailWorker` callers | engine (one file per spawned worker; records arrive via `Engine.WriteLog` and are routed by `category` prefix) |
| `%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\cache\<client>\…` (POSIX equivalent) | the writing client | writing client | writing client |
| `%LOCALAPPDATA%\autocontext\engine-metadata.json` (POSIX equivalent) — shared engine-liveness registry | every live engine (co-owned) | every engine on start/shutdown, every `Engine.GetSharedMetadata` caller | every engine append-updates its own row on start and removes its own row on graceful shutdown; never touches peer rows |

Three rules fall out and the implementation must enforce all three:

- **`Resources/` is read-only at runtime.** No engine-mutation,
  ever. Anything the engine wants to persist goes in
  `.autocontext.json`, the engine log file, or the OS user-cache
  root. Failure modes the rule prevents are listed under the
  [Pitfalls](#pitfalls) entry of the same name.
- **Clients never write under their own install directory.** Install
  dirs (`<extensionPath>`, `${CLAUDE_PLUGIN_ROOT}`) are read-only on
  managed installs and get wiped on host upgrade. All client-owned
  on-disk artefacts live under the OS user-cache root with the
  client's own subdirectory.
- **The engine never reads or cleans the client-cache root *of a live instance*.**
  Lifecycle of every file under a live instance's
  `…\autocontext\<workspaceHash>#<instanceId>\cache\…` is the
  writing client's contract with its host (extension storage,
  Anthropic session lifecycle, …). New client-owned subdirectories
  must be documented in the pitfall list with their owning client so
  cleanup responsibility stays unambiguous. Per-instance scoping
  means the engine never has to reason about cross-launcher
  contention either — each engine sees only the cache subtree under
  its own `<workspaceHash>#<instanceId>`. The single carve-out is
  the engine's own housekeeping sweep (see next rule): when the
  owning instance is verifiably dead and its retention window has
  elapsed, the cache subtree is orphaned by definition and any
  live engine doing its startup or shutdown sweep deletes it
  together with the rest of the per-instance subtree. The engine
  never touches the cache root of a *live* instance — not its own,
  not a peer's.
- **Per-instance subtree cleanup is the engine's own job, mediated
  by the shared liveness registry.** Every engine, on startup,
  writes its own row into `…\autocontext\engine-metadata.json` —
  one file shared by every live engine on the machine, carrying
  `{ workspaceHash, instanceId, instanceLabel, pid,
  processStartTimeUtc, engineVersion, startedAt, retention }`
  per row. The write is an upsert keyed on `instanceId`: any
  pre-existing row with the same `instanceId` (left behind by a
  prior crash-respawn inside the same launcher) is replaced,
  never duplicated. On graceful shutdown the engine removes its
  own row. A crash leaves the row stale; that is intentional,
  because staleness is exactly the signal the next engine's
  housekeeping sweep consumes. Writes use `FileShare.None` with
  exponential-backoff retry (same discipline as
  `.autocontext.json`); concurrent engine starts serialise on the
  handle, no engine ever rewrites another engine's row. The engine
  exposes the file's current contents over the wire as
  `Engine.GetSharedMetadata` (see the RPC surface section) for
  observability surfaces (`autocontext ps`-style listings, tree-view
  badges).

  The cleanup itself runs inside every live engine, on the
  two-clock schedule defined in `### Housekeeping`: a startup
  sweep before pipe-bind and a shutdown sweep after own-row
  removal. Each sweep pid-checks every row (`pid` exists AND
  `Process.StartTime` ≈ `processStartTimeUtc` within tolerance,
  to defeat pid recycling) and treats rows that fail the check as
  dead. Every `…\autocontext\<workspaceHash>#<instanceId>\`
  directory whose `<instanceId>` is not in the live set, *and*
  whose row's `retention` window has elapsed since `startedAt`,
  is orphaned and gets deleted (whole subtree — logs and cache).
  Retention is honoured per-row — the *dead* engine's declared
  `--retention` controls when its leftovers expire — so a
  long-retention engine's logs survive even if every subsequent
  engine declares a shorter window. Rowless subtrees (a crash
  before the row was flushed) fall back to the sweeping engine's
  own `--retention`.

  No external sweeper exists. Every engine spawn pays the
  housekeeping cost on behalf of every dead peer; the design
  refuses to rely on a CLI subcommand the user has to remember to
  run. See [autocontext-cli.md](./autocontext-cli.md) for the CLI surface
  the engine actually exposes.

### P6. Subscriptions are first-class; clients never poll or watch

Every observable engine state has a `*.Subscribe` channel with the
same shape (`Config.Subscribe`, `Instructions.Subscribe`,
`Engine.Lifecycle.Subscribe`, `Agent.Events.Subscribe`):

- **Server-streaming**, one channel per topic.
- **Emits a current-state snapshot on subscribe** so a late subscriber
  never has to ask "what's the current value?" separately.
- **Carries a generation counter** wherever cache invalidation
  matters; clients invalidate on counter change without diffing
  payloads.
- **Lossless within a live subscription, rehydrated on reconnect.**
  The pipe transport delivers in-order while the subscription is
  live; clients that disconnect and reconnect rely on the
  snapshot-on-subscribe rule above to catch up. There is no
  persistent event queue or replay log. UX-only event families
  (`Agent.*`) tolerate lost events even on a live subscription;
  content and lifecycle channels do not, because losing them would
  leave the client with no other path to reconstruct the state.

Clients never poll, never watch files, never re-derive state from
disk. New observable state gets a `*.Subscribe` channel; no
client-side watcher is added.

### P7. Two-layer matching: coarse on the producer, fine on the consumer

When a match decision spans engine state **and** host-native
semantics, the engine performs a coarse set-intersection that **must
remain a strict superset** of any client's fine match; the client
uses its host-native matcher unchanged.

- The seed case is `applyTo` × workspace files: engine intersects the
  internally-derived per-file extension set against
  `Workspace.Detect.extensions`; clients run host-native glob
  matchers (`vscode.languages.match`,
  `Microsoft.Extensions.FileSystemGlobbing`, `minimatch`) on the raw
  `applyTo` string.
- Crossing the streams is forbidden in both directions: pushing fine
  matching into the engine drifts from editor semantics; pushing
  coarse matching into the client re-derives the workspace extension
  set N times across N hosts and drifts the same way.
- The engine never tries to re-implement host-native semantics; the
  client never re-derives engine-owned indices. Any future match-like
  capability (content, path-prefix, identifier) follows the same
  split.
- **The coarse layer parses; it does not normalise.** Parsed
  structure is engine-internal (P3); the client receives the raw
  source and matches it with native semantics.

## Composition contracts

Only two surfaces from the composition layer are part of the design;
everything else is implementation choice that the implementation plan
owns.

- **`IHostApplicationBuilder.AddAutoContextEngine(Action<EngineOptions> configure)`**
  is the engine library's single public entry point. The
  `autocontext-engine` `Program.Main` calls it; tests call it; nothing
  else does. `EngineOptions` exposes the four CLI-surfaced knobs
  (workspace path, idle timeout, MCP-server mode, version display)
  *and* library-only knobs that deliberately don't surface on the
  command line (corpus root override, pipe-name override). The CLI
  surface is locked to the four switches enumerated under
  `### Engine options`; everything else is reachable only through
  in-process composition. See that section for the rejection rule
  and the rationale for keeping pipe-name override off the binary's
  argv (P4).
- **`AutoctxClient` (TS, `Framework.Web/src/cli/`)** is the only
  shared TS class. Plain class, no DI container, constructed with
  `new` and a workspace path. Speaks the same wire protocol the
  .NET engine serves; that wire protocol is the cross-host seam,
  *not* a class hierarchy.

The extension and the plugin do not share a composer; they share
the engine **binary** (one process per workspace) and the **wire
protocol** (consumed by `AutoctxClient` on the TS side).

## Project layout

The engine and the client dialer are two *libraries*, not two
sub-folders of one library, and the binaries that host them are
thin. Three .NET library projects under `src/`, plus one host
project per binary that exists only to call `Main`:

```
                AutoContext.Framework
        (Pipes, Logging, Hosting, Workers, Protocol)
                  ▲                     ▲
                  │                     │
   AutoContext.Framework.Engine   AutoContext.Framework.Client
                  ▲                     ▲
                  │                     │
   AutoContext.Engine (binary)     AutoContext.CommandLine (binary)
   → autocontext-engine[.exe]      → autocontext[.exe]
```

- **`AutoContext.Framework`** is the shared substrate every
  AutoContext .NET process already depends on — `Pipes`, `Logging`,
  `Hosting`, `Workers` (see *What `AutoContext.Framework` carries
  over*). One new sub-namespace lands here:
  - **`AutoContext.Framework.Protocol`** holds the **cross-side DTOs**
    both libraries marshal: the protocol-version integer constant
    `Engine.Hello` exchanges, the pipe-name builder (`rpc` /
    `events` / `health` / `logs` × workspace-hash × instance-UUID
    — P4), and the discriminated-union envelopes that appear on
    *both* sides of every RPC (`Instructions.Get` /
    `McpTools.Invoke` / `Engine.GetSharedMetadata` row / the
    `Engine.WriteLog` log-record envelope). Neither library can
    own these without the other depending on it; they belong with
    the substrate.
- **`AutoContext.Framework.Engine`** is the engine **as a library**.
  Everything under `### Engine-internal services` lives here
  (`AutoContextConfigStore`, `InstructionsCorpusService`,
  `InstructionsFileBodyProjector`, `InstructionsListBuilder`,
  `InstructionsContentIndex`, `WorkspaceContextDetector`,
  `WorkerManager`), together with the pipe-server bindings for the
  four pipes and the RPC handlers (one per capability — P1). Public
  surface is `IHostApplicationBuilder.AddAutoContextEngine(Action<EngineOptions>)`
  (see *Composition contracts*).
- **`AutoContext.Framework.Client`** is the dialer **as a library**.
  Pipe-client plumbing for the four pipes, typed clients for every
  RPC surface (`Instructions.*`, `Config.*`, `Workspace.*`,
  `McpTools.*`, `Discovery.*`, `Agent.*`, `Logs.*`), and the
  subscription-stream consumers (`Engine.Lifecycle.Subscribe`,
  `Config.Subscribe`, `Instructions.Subscribe`,
  `Agent.Events.Subscribe`). Public surface mirrors the engine's:
  `IHostApplicationBuilder.AddAutoContextClient(Action<ClientOptions>)`.
  This is the .NET counterpart of TS `AutoctxClient` (see the
  *Sharing principle* — both sides dial the same wire; the
  `AutoctxClient` plain TS class and `AutoContext.Framework.Client`
  on .NET are parallel implementations of one wire contract, not
  derivations of one shared abstraction).
- **`AutoContext.Engine` (binary)** is the engine host. `Program.Main`
  parses argv per `### Engine options`, calls
  `AddAutoContextEngine(...)`, runs the host. Published per-RID as
  `autocontext-engine[.exe]` (see *Distribution*).
- **`AutoContext.CommandLine` (binary)** is the CLI host. `Program.Main`
  parses subcommands (see [autocontext-cli.md](./autocontext-cli.md)), calls
  `AddAutoContextClient(...)`, dispatches verbs. Published per-RID
  as `autocontext[.exe]`.

**Neither library references the other.** `AutoContext.Framework.Engine`
binds pipes and serves RPCs; `AutoContext.Framework.Client` dials
pipes and consumes RPCs. The only thing they share is
`AutoContext.Framework.Protocol`. Two binaries hosting both libraries in
one process is technically possible (a hypothetical "thick engine
that also dials a peer") and structurally permitted, but no shipped
binary does this today and the layout makes the asymmetry visible
to anyone reading the project graph.

**Sharing-principle caveat.** This split must not become an excuse
to introduce portability abstractions — no `IFileSystem`, no
`IWorkspace`, no engine/client-agnostic "AutoContext core"
interface set the two libraries program against. The engine *binds*
pipes, the client *dials* them; that asymmetry is intentional and
the wire is the only seam (`## Sharing principle`). Library
boundaries enforce direction-of-flow at the project graph level;
they do not invite a third "shared logic" layer between Framework
and the two halves.

**Workers are unchanged.** Each `AutoContext.Worker.*` project
references `AutoContext.Framework` only — specifically `Workers`
(hosting scaffold), `Logging` (record producer), and `Protocol` (log
envelope, pipe-name builder for dialling the engine's `logs` pipe
via `Engine.WriteLog`). Workers do not reference
`AutoContext.Framework.Engine` (they are spawned *by* it, not
hosted *in* it) and do not reference `AutoContext.Framework.Client`
(they speak a narrower wire than full RPC clients do).

**Test-project layout** mirrors the library split:

| Test project | Covers |
|---|---|
| `AutoContext.Framework.Tests` | Shared substrate — `Pipes`, `Logging`, `Hosting`, `Workers`, `Protocol` envelope round-trips |
| `AutoContext.Framework.Engine.Tests` | Engine-internal services, RPC handlers, pipe-server bindings; absorbs today's `AutoContext.Mcp.Server.Tests` |
| `AutoContext.Framework.Client.Tests` | Typed RPC clients, subscription-stream consumers, dialer back-pressure / reconnect behaviour |
| `AutoContext.Worker.*.Tests` | Unchanged — per-worker task suites against the testing harness |

`AutoContext.Mcp.Server.Tests` is retired into
`AutoContext.Framework.Engine.Tests` (the MCP server *is* the
engine — see *What the engine absorbs from today's topology*).

**Future subset-library carve-out is a possibility, not v1.**
A consumer that wants only the corpus projection — say, a static
documentation generator that wants `InstructionsContentIndex` and
`InstructionsFileBodyProjector` without any pipe-server machinery
— could be served by a future `AutoContext.Framework.Instructions`
slice carved out of `AutoContext.Framework.Engine`. This is
explicitly **not** a v1 split. Pre-splitting on speculative
embedding scenarios produces more boundaries to maintain than
consumers to serve; the carve-out lands the day a real consumer
asks for it.

## Distribution

The engine must be discoverable from a cold Anthropic plugin
SessionStart hook (no VS Code extension running, no PATH guarantee).
Decision:

- `autocontext-engine` is published per-RID by `dotnet publish -r <rid>
  --self-contained` from `build.ps1 Package`. No Node runtime is
  bundled; the engine and every subcommand are pure .NET.
- **Supported RIDs:** `win-x64`, `win-arm64`, `linux-x64`,
  `linux-arm64`, `osx-x64`, `osx-arm64`. Resolved at runtime from
  `process.platform` + `process.arch` on the TS side and from the
  bundled binary path on the .NET side. Unsupported combinations
  surface a hard error from the spawner; there is no in-process
  fallback path.
- Per-platform shipped artefact (the **same** layout in every
  target). Build output stages per-RID under
  `out/engine/<rid>/...`; per-platform packaging
  (`vsce package --target <target>` for the VSIX, the equivalent
  per-platform plugin release, one GitHub-release tarball per RID)
  selects the matching `<rid>/` and copies its contents into
  `engine/` in the shipped artefact. The user-visible bundle has
  **no `<rid>/` segment**:

  ```
  engine/
    autocontext-engine[.exe]                       # engine binary (this doc)
    <framework dlls / runtime files>               # self-contained .NET runtime for the engine
    Instructions/                                  # curated corpus (read-only side-car)
      <name>.instructions.md
    Resources/                                     # build-generated read-only manifests
      instructions-files.json                      # wire-shape catalogue for Instructions.List
      instructions-files-metadata.json             # engine-internal indices (section maps,
                                                   #   parsed applyTo extension sets,
                                                   #   pre-computed content-index seed)
      mcp-tools.json                               # wire-shape catalogue for McpTools.List
      mcp-tools-registry.json                      # source-of-truth tool→worker dispatch table
      mcp-tools-registry-schema.json               # JSON-schema for the registry
      workers.json                                 # build-generated worker manifest
    Workers/                                       # per-worker subdir, key = workers.json `id`
      workspace/AutoContext.Worker.Workspace[.exe] # self-contained per-RID dotnet worker
        <its runtime files>
      dotnet/AutoContext.Worker.DotNet[.exe]
        <its runtime files>
      web/index.js                                 # node worker (Node runtime is host-supplied)
        <its bundle / node_modules>
  ```

  Three flat side-car directories sit next to the engine binary:
  `Instructions/` (curated corpus), `Resources/` (read-only JSON
  manifests), `Workers/` (per-worker subdirs). Capitalisation
  follows .NET resource-folder convention; JSON filenames are
  kebab-case. The engine resolves each from
  `AppContext.BaseDirectory + "<Dir>"` without any host-supplied
  path — because the shipped layout has no `<rid>/` segment, the
  resolver is a clean one-segment join with no `..` traversal.
  The corpus and manifests are RID-independent in content; they
  appear once per shipped artefact (no duplication across RIDs on
  any user's machine, because each user installs exactly one
  per-platform VSIX / plugin release / tarball).
  **Each worker lives in its own `Workers/<id>/` subdir** so
  per-worker self-contained runtimes
  (`dotnet publish -r <rid> --self-contained`) do not collide with
  each other or with the engine's runtime files at the `engine/`
  root. The `autocontext` CLI is **not** in this layout — it ships in
  its own bundle (see [autocontext-cli.md](./autocontext-cli.md)) and
  carries its own copy of the engine if it needs to spawn one.
- Bundle locations:
  - `<vsix>/engine/...` for the VS Code extension.
  - `<plugin-root>/engine/...` for the Anthropic plugin.
- Hosts resolve the engine binary by joining the resolved root
  (`extensionPath` for VS Code, `${CLAUDE_PLUGIN_ROOT}` for the
  plugin) with `engine/autocontext-engine[.exe]`. No PATH
  dependency, no current-RID lookup at dial time — the shipped
  artefact already matches the platform it was packaged for.
- A standalone GitHub release publishes one tarball per RID with the
  same flat `engine/` layout for users who want to run
  `autocontext-engine` directly.

### Resource manifests

Everything under `Resources/` is **read-only side-car data generated
or copied at build time, parsed by the engine at startup, never
written back by the engine**. The engine projects per-request
against workspace state (disabled rules, overrides) instead of
mutating the manifests.

- **`instructions-files.json`** — build-generated by
  `InstructionsListBuilder` over the curated corpus. Carries every
  field the `Instructions.List` envelope returns (Issue #4) **except**
  fields that depend on workspace state: `disabled` (resolved per
  request from `.autocontext.json`) and `source`/`overridePath?`
  (resolved per request from `<workspace>/.github/instructions/`).
  The engine reads this manifest once at startup, holds it in
  memory, and re-projects per request as workspace state changes.
- **`instructions-files-metadata.json`** — build-generated companion
  used **engine-internally only**: pre-computed section anchor maps,
  parsed `applyTo` extension sets (the engine-internal output of
  the Issue #7 parser), and the content-index seed that
  `InstructionsContentIndex` rehydrates at startup. Not returned on
  the wire; the wire shape is `instructions-files.json`. Splitting
  the two keeps `instructions-files.json` round-trippable against
  the public `Instructions.List` envelope and lets the internal
  indices evolve without breaking the wire contract.
- **`mcp-tools.json`** — build-generated wire-shape catalogue for
  `McpTools.List`, projected from `mcp-tools-registry.json` at
  build time. The engine reads this file directly when answering
  `McpTools.List`; per-request projection only applies the
  `disabledTools` / `disabledTasks` filter.
- **`mcp-tools-registry.json`** — source-of-truth tool→worker
  dispatch table (renamed from today's `mcp-workers-registry.json`).
  Drives the engine's worker dispatch for `McpTools.Invoke`
  (Issue #8) and the build-time projection that writes
  `mcp-tools.json`. Schema-validated at build time against the
  sibling `mcp-tools-registry-schema.json`; the schema file ships
  alongside the registry so external tooling (CI lint, IDE
  intellisense in `mcp-tools-registry.json` itself) can validate
  without reaching into the source tree.
- **`workers.json`** — build-generated worker manifest. Scans
  `src/AutoContext.Worker.*/` projects, derives `id` by stripping
  the `AutoContext.Worker.` prefix and replacing `.` with `-` and
  lowercasing (`AutoContext.Worker.DotNet` → `dotnet`,
  `AutoContext.Worker.DotNet.Roslyn` → `dotnet-roslyn`), derives
  `type` from the project file (`.csproj` → `dotnet`,
  `package.json` → `node`), and writes the actual published
  `entrypoint` path relative to `engine/`. Id collisions fail
  the build. Shape:

  ```json
  {
    "workers": [
      { "id": "workspace", "type": "dotnet",
        "name": "AutoContext.Worker.Workspace",
        "entrypoint": "Workers/workspace/AutoContext.Worker.Workspace.exe" },
      { "id": "dotnet", "type": "dotnet",
        "name": "AutoContext.Worker.DotNet",
        "entrypoint": "Workers/dotnet/AutoContext.Worker.DotNet.exe" },
      { "id": "web", "type": "node",
        "name": "AutoContext.Worker.Web",
        "entrypoint": "Workers/web/index.js" }
    ]
  }
  ```

  The `entrypoint` is written by the build (not derived by the
  engine via convention) so a worker can change its assembly name
  or entrypoint file without touching the engine's resolver.

Source-side locations for the editable inputs the build consumes:

- `src/AutoContext.Engine/Instructions/` — editable curated corpus.
- `src/AutoContext.Engine/Resources/mcp-tools-registry.json` (+
  `mcp-tools-registry-schema.json`) — hand-edited registry and its
  schema. The build copies them as-is into the per-RID staging
  `Resources/` dir.
- `Resources/instructions-files.json`,
  `Resources/instructions-files-metadata.json`,
  `Resources/mcp-tools.json`, and `Resources/workers.json` have **no
  source-side copy** — they are pure build outputs, regenerated
  every package run.

## Pitfalls

- **Engine termination signal.** `autocontext-engine` is launched
  detached, with no inherited stdio handles — every spawner
  (the VS Code extension and Anthropic plugin via Node
  `child_process.spawn(..., { stdio: 'ignore', detached: true })`,
  the `autocontext` CLI via .NET `Process.Start` with
  `UseShellExecute = false` and redirected/null stdio) deliberately
  cuts the engine off from a controlling console so it can outlive
  the spawner. Consequence: `Console.CancelKeyPress` does not
  fire inside the engine. Production termination is
  `--idle-timeout` plus the OS-level signal path
  (`AppDomain.ProcessExit` for SIGTERM / Windows stop). Foreground
  invocations (smoke tests, `dotnet run`) reach the SIGINT path
  normally because they keep the console attached.
- **MCP/stdio idle-timeout interaction.** The implementation trap:
  the idle watchdog must count an active stdio connection toward
  the keep-alive gate exactly like an `rpc` or `events` pipe
  connection, or an MCP-only session shuts the engine down
  mid-conversation. Conversely, without `--mcp-server` the engine
  must **not** register the MCP SDK's stdio transport at all —
  non-MCP spawners pass `stdio: 'ignore'` (stdin → `/dev/null`)
  and an unconditional `WithStdioServerTransport()` would hit
  immediate EOF and self-terminate. See
  [Lifecycle](#lifecycle) > *MCP/stdio facade* for the canonical
  behaviour.
- **`autocontext-engine --version` is RID-independent.** Driven by
  `AssemblyInformationalVersionAttribute` set from `version.json`;
  do not bake the RID into the version string. The corpus and the
  version are RID-independent in content.
- **Engine-owned on-disk artefacts.** The engine writes its
  on-disk artefacts in two places. The per-instance subtree
  `%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\`
  (Windows; equivalents under the OS user-cache root on POSIX)
  holds the engine-written log files under the `logs\`
  subdirectory: `engine.log` for engine-emitted records,
  `errors.log` (future) for unhandled-exception / fatal-startup
  output, and one `worker-<workerId>.log` per spawned worker
  receiving worker-emitted records that arrive via `Engine.WriteLog`
  and are routed by `category` prefix (see the *Log pipeline
  backpressure* and *Worker–engine connectivity* pitfalls). Every
  file under `logs\` is engine-owned per P5 — the engine is the
  sole writer — and is rotated in-process by the `--logging`
  thresholds (see `### Housekeeping` > *Log rotation*), with
  rotated history pruned per `--retention`. Active files survive
  shutdown for postmortem reading by anyone who knows the
  launcher's UUID; rotated files survive until retention elapses.
  No engine-owned cache
  directory exists — every engine cache is in-memory and invalidates
  on internal events. The sibling `cache\` subdirectory under the
  same per-instance root is **client-owned**: the writing client is
  responsible for its lifecycle, and the engine neither reads nor
  cleans those paths while the instance is live. Outside the per-instance subtree,
  one shared file at the autocontext root —
  `%LOCALAPPDATA%\autocontext\engine-metadata.json` (POSIX
  equivalent) — is co-owned by every live engine on the machine:
  each engine writes its own row on start (replacing any prior
  row with the same `instanceId` from a crash-respawn) and
  removes its own row on graceful shutdown, never touching peer
  rows. A crash leaves the row stale on purpose, because that is
  the signal the next engine's housekeeping sweep uses to identify
  orphaned instances (see P5 and `### Housekeeping`).
  Clients must never cache under their own install directory
  (`<extensionPath>`, `${CLAUDE_PLUGIN_ROOT}`) — those are
  read-only on managed installs and get wiped on host upgrade.
  Document any new client-owned subdirectory in this list with
  its owning client so cleanup responsibility stays unambiguous.
  Per-instance subtree sweeping for orphaned
  `…\autocontext\<workspaceHash>#<instanceId>\` directories is the
  engine's own startup/shutdown housekeeping job, mediated by the
  shared registry (see P5).
- **`engine-metadata.json` row lifecycle: write-on-start,
  remove-on-graceful-shutdown, leave-stale-on-crash.** Every engine
  writes its own row to the shared registry as part of startup
  (after pipe bind, before accepting connections), upserting on
  `instanceId` so a crash-respawn inside the same launcher
  replaces the prior stale row rather than appending a duplicate,
  and removes its own row from the `AppDomain.ProcessExit` /
  SIGTERM / Windows service-stop path on the way out. A crash, kill -9, or power loss
  leaves the row in place; this is **intentional**, because the
  staleness signal is exactly what the engine's housekeeping sweep
  consumes to identify orphaned per-instance subtrees. Two pitfalls follow.
  First, pid recycling: a row's `pid` field on its own is not
  enough to assert liveness, because the OS may have recycled the
  pid to a different process by the time the registry is read. The
  row carries `processStartTimeUtc` alongside `pid`, and any consumer
  asserting liveness (including the engine itself when answering
  `Engine.GetSharedMetadata` for diagnostic callers, and especially
  the housekeeping sweep when deciding what to delete) must compare
  `Process.GetProcessById(pid).StartTime` against
  `processStartTimeUtc` with a small tolerance (~1 s for clock
  jitter); mismatch means the pid was recycled and the row is
  stale. Second, registry write contention: two engines starting
  concurrently both want to append their row. Writes use
  `FileShare.None` plus exponential-backoff retry (same discipline
  the engine already uses for `.autocontext.json`), so the OS
  serialises the appends and neither engine corrupts the file. A
  corrupt-file recovery path exists for the case where a write was
  interrupted mid-flush: any engine encountering an unparseable
  registry on startup truncates it and writes only its own row,
  on the theory that one re-derivable file is cheaper than
  blocking startup forever. The housekeeping sweep encountering the
  same corrupt file treats every per-instance subtree as orphaned
  (because the registry can no longer attest to any liveness) and
  proceeds against retention as usual; the next engine start
  re-seeds the file.
- **Log pipeline backpressure: workers never block, slow subscribers
  never starve.** The unified-logging design routes every worker
  `ILogger<T>` record through `Engine.WriteLog` into the engine's
  single sink, then fans it out on the `logs` pipe. Two
  failure-modes must not couple. (a) Worker → engine ingest is
  **fire-and-forget with a bounded in-memory buffer; drop oldest on
  overflow**. The worker's `Engine.WriteLog` notification never
  awaits an ack and never throws back into the caller's `ILogger<T>`
  call site — a busy worker that out-runs the engine's ingest rate
  loses its oldest queued records, and the engine emits a single
  `category: "engine.logging"`, `level: "warning"` record
  (`"dropped N worker log records"`) the next time the queue drains,
  so the gap is visible without flooding the log. (b) Engine →
  `logs`-pipe fan-out is **non-blocking per subscriber**: each
  subscriber has its own bounded send buffer, and a subscriber that
  cannot keep up is disconnected (its pipe handle closed) rather
  than allowed to back-pressure the file sink, the engine's
  ingest loop, or any other subscriber. The `logs` pipe is
  passive-observer (P4) precisely because losing a subscriber must
  never affect engine progress. Both rules are non-negotiable:
  inverting either one re-introduces the every-component-has-its-own-log
  problem the unified sink is solving.
- **Worker–engine connectivity: bounded in-memory buffering, no
  on-disk worker spool.** The worker side of `Engine.WriteLog` is
  fire-and-forget with a bounded in-memory ring, not a durable
  spool. The worker's `AddEngineLoggerProvider()` registers an
  `ILogger<T>` provider that — on reachable engine — serialises
  the record, frames it, writes it to the pipe, and forgets it;
  on unreachable engine (engine mid-shutdown, RPC pipe unbound
  during a startup race, worker still inside cold-start) it
  enqueues the record into a bounded ring (default 1000 records or
  1 MiB, whichever fills first; drop-oldest on overflow), retries
  the pipe at exponential backoff, and drains the ring on the
  next successful write. On worker process exit whatever is still
  in the ring is **lost** — there is no on-disk worker-owned spool
  and no replay file the next engine pickup drains. This is the
  deliberate trade-off: `Engine.WriteLog` records are dev-tool
  diagnostics, not flight-recorder data, and engine-only ownership
  of `…\logs\` is worth the loss-on-disconnect-then-crash window.
  On overflow / drop the worker writes one line to **stderr** per
  drop batch (`"engine log dropped N records"`); the engine
  supervises every worker it spawns via `Process.Start` and folds
  captured stderr into `worker-<workerId>.log` under category
  `worker.<workerId>.engine.stderr`, so the *failure to log* is
  itself observable through the engine's own log even when the
  channel that would normally surface it is the broken one. The
  ring bound is intentionally small. A worker that genuinely
  cannot reach its engine for long enough to fill 1000 records is
  in a degraded state the design does not try to make invisible;
  the dropped-record stderr line plus a future health signal
  ("worker spawned but no records received in N seconds") are the
  right surfaces for that condition, not a growing on-disk spool.
- **Cross-instance `.autocontext.json` writes race on disk, not
  on the wire.** Two launchers on the same workspace run two
  independent engines (different `<instanceId>`) with independent
  in-memory state. Both treat `.autocontext.json` as the authority
  of record across instances: writes must take a `FileShare.None`
  handle with exponential-backoff retry, and reads pick up peer
  changes through the engine's existing `FileSystemWatcher` and
  surface as a regular `Config.Subscribe` event. Engines never
  RPC each other — cross-instance change propagation is
  filesystem-mediated only. The visible consequence is a small
  divergence window: an instruction toggle in one VS Code window
  reaches a second VS Code window on the same workspace only
  after the file write completes and the watcher debounce fires.
  Within a single launcher instance there is no divergence — the
  extension and its hooks share the same engine.
- **Hook scripts outside a known launcher need explicit
  `--instance-id` propagation.** Hooks running under the same
  host process that *spawned* a launcher (VS Code Copilot hooks
  inside the AutoContext-extension's VS Code window, Claude Code
  hooks inside that Claude session) inherit the launcher's
  instance UUID through whatever side channel the host already
  uses to pass per-session state — typically an environment
  variable the launcher sets on its own process before spawning
  any child. Hooks running under a host the AutoContext launcher
  cannot reach (a stand-alone agent invocation, a future host
  that does not run alongside one of our clients) have no
  launcher to inherit from and must spawn their own engine with
  a hook-minted UUID. The trap is the in-between case — a hook
  script that *could* reach an existing launcher's engine but
  doesn't, and silently spins up a parallel engine on the same
  workspace, fragmenting `Agent.*` broadcast and burning an
  extra idle-timeout countdown. Hook templates surface the
  propagated UUID prominently; the engine itself does nothing to
  detect this case.
- **Override survival across upgrades.** A workspace-local
  `<workspace>/.github/instructions/<name>.instructions.md` keeps
  winning silently when the bundled source updates in a release.
  The corpus service emits a warning event when override mtime is
  older than bundled mtime; UIs surface it as a non-fatal hint.
- **`Resources/` is read-only at runtime.** (Instance of **P5**.) The engine reads every
  side-car JSON manifest under `engine/Resources/` at startup
  and projects per-request against workspace state
  (`.autocontext.json`, override files, generation counter). It
  **never writes back** to any file under `Resources/` — not to
  patch a disabled flag, not to record a generation bump, not to
  cache anything. Two failure modes this rule prevents: (a) managed
  installs (VSIX, Anthropic plugin) mount their install dir
  read-only or wipe it on upgrade, so a write would either fail at
  runtime or silently disappear; (b) when multiple launchers on
  the same workspace each spawn their own engine, every engine
  reads the same install-time-immutable side-cars, so any
  per-workspace mutation routed through `Resources/` would have
  to be re-applied by every peer to stay consistent — the
  consistency problem the engine consolidation eliminated would
  reappear at the file-system layer. State the engine wants to
  persist goes in workspace state
  (`.autocontext.json`), engine log files under the per-instance
  subtree
  (`%LOCALAPPDATA%\autocontext\<workspaceHash>#<instanceId>\logs\engine.log`),
  or the OS user-cache dir — never in `Resources/`.
- **`alwaysAttached` is explicit, not derived.** The set returned
  by `Instructions.GetAlwaysAttached` is the files whose
  frontmatter declares `alwaysAttached: true`, not the files
  whose frontmatter omits `applyTo`. Today's corpus has six files
  with no `applyTo` (`copilot`, `autocontext`, `code-review`,
  `design-principles`, `git-commit`, `rest-api-design`); only
  the first two are always-attached. The other four are
  domain-conditional and surface via `Discovery.RouteForPrompt`.
  Engine implementations must not collapse "no `applyTo`" into
  "always-attached" — that would flood every turn with editorial
  guidance the user did not ask for. Override files inherit the
  flag from the bundled source they shadow; an override cannot
  promote or demote a file in or out of the always-attached set.
- **`Instructions.Get` distinguishes `disabled` from `not-found`.** (Instance of **P2**.)
  Both outcomes return no body, but they mean different things:
  `not-found` says the name was never in the corpus (typo, stale
  reference, removed file); `disabled` says the file exists and
  the user has actively muted it. LM-facing surfaces — Copilot's
  `get_autocontext_instructions_file`, future MCP equivalents,
  any tool with a model in the loop — must surface the disabled
  envelope verbatim so the model can tell the user "this rule
  exists but is muted" instead of silently ignoring the request.
  Consumption-mode surfaces with no UI for the user (sub-agent
  materialisation, `additionalContext` injection,
  `GetAlwaysAttached`) treat both outcomes as omission. A client
  that flattens `disabled` to `not-found` (or vice versa) breaks
  the user's mental model of what their `.autocontext.json`
  toggles actually do.
- **LM-tool and MCP-tool handlers are marshalling shims, not
  business logic.** (Instance of **P1**.) The instruction-discovery LM tools registered by
  the VS Code extension and their `instructions_*` MCP-tool
  counterparts on the engine's MCP/stdio facade are paper-thin: they
  deserialise input, call the corresponding engine RPC handler, and
  serialise the result. Trigram indexing, override resolution,
  metadata predicate evaluation, coarse `applyTo` filtering (see
  the next pitfall for the coarse/fine split), and disabled filtering
  all live inside the engine's service layer; both surfaces share
  one implementation. The failure mode this rule
  prevents is cross-surface drift — a field added to the MCP-tool
  schema but not to the LM-tool shim (or the reverse), or a filter
  applied in one transport but not the other, re-introducing the
  two-surfaces-could-diverge problem the engine consolidation
  eliminated. If a field can't be expressed on the engine RPC, it
  can't be expressed on either tool surface either.
- **`McpTools.Invoke` and MCP `tools/call` share one handler, one
  validator, one worker dispatcher.** (Instance of **P1**.) The pipe-RPC `Invoke` method
  is a marshalling shim parallel to the instruction-discovery shims
  above — input deserialisation and result serialisation only.
  `inputSchema` validation, override of the worker-pipe dispatcher,
  cancellation forwarding, and `disabled` / `not-found` filtering
  all live in the engine's service layer; the MCP/stdio `tools/call`
  handler is the same shim with a different transport. Resist the
  temptation to give the pipe surface a "richer" envelope than MCP
  — extra structured error fields the MCP spec doesn't carry,
  pipe-only progress channels, pipe-only timeout knobs — because
  every such asymmetry re-opens the two-surfaces-could-diverge
  problem. The discriminated `InvokeResponse` already encodes every
  distinction the pipe needs (`ok` / `tool-error` / `schema-error` /
  `disabled` / `not-found`), and `content: ContentBlock[]` matches
  MCP's `CallToolResult.content` block-for-block so a recorded pipe
  response can be diffed against a recorded stdio response and
  produce zero output.
- **`applyTo` matching: engine does coarse, client does fine. Never
  cross the streams.** (Instance of **P7**.) The engine's coarse layer is a set
  intersection of extension sets (the engine's internally-derived
  per-row extension set ∩ `Workspace.Detect.extensions`); the
  client's fine layer is glob
  × glob matching via its host's native matcher
  (`vscode.languages.match` in VS Code, `Microsoft.Extensions.FileSystemGlobbing`
  in the CLI, `minimatch` in hooks). Pushing fine matching into the
  engine introduces silent drift from editor semantics — a
  hand-rolled .NET glob library will not be byte-identical to
  `vscode.languages.match` for `**` greediness, case sensitivity,
  `.gitignore`-excluded paths, or brace expansion edge cases.
  Pushing coarse matching into the client re-derives the workspace
  extension set N times across N hosts and drifts the same way.
  The coarse layer must remain a strict superset of any client's
  fine result — if the engine excludes a row the client would have
  matched, the entire surface goes inconsistent.
- **The `applyTo` parser parses, it does not normalise, and its
  output is engine-internal.** (Instance of **P3**.) The engine-side `applyTo` parser
  (next to `InstructionsListBuilder`, runs at build time and feeds
  the same parsed extension set into
  `Resources/instructions-files-metadata.json`) splits comma-separated
  lists,
  trims whitespace, brace-expands `{a,b,c}` groups, and extracts an
  extension set. The result is consumed by the engine's coarse
  `applyTo` filter and by `Discovery.RouteForPrompt`'s extension
  index; it is **not** emitted on the `Instructions.List` wire
  envelope. Clients receive only the raw `applyTo` string and hand
  it to their host-native fine matcher unchanged. The parser must
  **not** attempt to canonicalise globs, simplify `**` patterns,
  deduplicate semantically-equivalent globs, or otherwise reason
  about what a glob means — that is matching, and matching is the
  client's job. An internal round-trip check (recomposed glob list
  equals the source `applyTo` modulo whitespace) must hold for
  every entry in the corpus; if a parser change ever breaks the
  round-trip, the parser is doing too much. Resist the temptation
  to publish the parsed structure as a convenience field on the
  wire — every client that ever wanted it could trivially derive
  it from the raw string, and publishing it locks in details the
  engine should be free to change.
- **Engine bootstrap is the chicken-and-egg.** The Anthropic plugin
  SessionStart hook runs before any extension. The engine must be
  self-spawning from a cold hook invocation — do not design a flow
  that requires the VS Code extension to start it first.
- **Pipe-name collisions across UNC / case-variant paths.**
  Normalise the workspace path (lowercase on Windows, resolve
  symlinks) before hashing for the pipe name; otherwise two hosts
  on "the same" workspace get different engines.
- **Concurrent first-connect.** Two hosts racing to spawn the
  engine will both spawn one. The second engine must detect the
  existing pipe on startup and exit cleanly (idempotent bind).
- **Corpus drift between RIDs.** The corpus is duplicated per RID
  in the packaged artefact. The build must copy from one source
  (`src/AutoContext.Engine/instructions/`) into every RID staging
  dir; no per-RID corpus edits are permitted. Validator asserts
  byte-equality across RIDs in a build.
- **Do NOT** port the engine to TypeScript. The engine is .NET; the
  TS side ships only `AutoctxClient` and the existing pipe
  transport.
- **Do NOT** invent cross-host portability seams. Using
  `Microsoft.Extensions.Hosting` (`IHostEnvironment`, `ILogger<T>`,
  `IOptions<T>`, `IConfiguration`) inside the engine is expected
  and matches the rest of the .NET solution. What we don't do is
  invent a custom `IFileSystem` / `IWorkspace`-style interface that
  pretends the C# engine and the TS extension share code — they
  share a wire protocol, not a class hierarchy. The TS-side
  `AutoctxClient` stays a plain class, no DI container.
- **Do NOT** fold workers into the engine. Workers are transient
  task executors with their own crash / lifecycle profile. The
  engine spawns them via the same lazy `ensureRunning(workerId)`
  gate `WorkerManager` uses today; workers stay as separate
  binaries (`AutoContext.Worker.DotNet`,
  `AutoContext.Worker.Workspace`, `AutoContext.Worker.Web`).
- **Do NOT** add a `service` URI subcommand to the CLI to launch
  the engine or workers. The engine binary is launched directly
  (by an MCP host or by `autocontext`'s spawner). Workers are launched
  by the engine. There is no `autocontext service ...` user surface.

## Implementation phase shape

The design doc records only the *shape* of the rollout below.

Shape:

- **Skeleton.** `AutoContext.Engine` project, empty
  `AddAutoContextEngine`, `autocontext-engine --version`, sibling
  `AutoContext.CommandLine` skeleton.
- **Engine library populated.** Config store, corpus reader,
  projector, corpus service, workspace detection, pipe-listener /
  idle-watchdog hosted services, RPC handlers, MCP-tool catalogue,
  worker dispatch, MCP/stdio facade. `EngineRpcClient` (.NET) /
  `AutoctxClient` (TS) companions.
- **MCP server retirement.** `AutoContext.Mcp.Server`'s
  `Program.Main` shrinks to delegating into `AddAutoContextEngine`,
  then is deleted entirely once nothing references it. The MCP host
  servers manifest is repointed at `autocontext-engine`.
- **Extension migration.** The four sideband pipe servers
  (`LogServer`, `HealthMonitorServer`, `WorkerControlServer`,
  `AutoContextConfigServer`) are deleted from the extension. The
  in-extension projection / config / corpus classes are deleted in
  the same release that ships the engine. The extension becomes a
  pure `AutoctxClient` consumer plus VS Code-specific UI.
- **Anthropic plugin re-pointing.** SessionStart and any other
  hooks call `AutoctxClient` against the engine pipe. Hooks
  surface `Engine.Hello` failure as a structured hook error;
  there is no in-hook disk-read fallback (engine and plugin
  ship versioned together inside the plugin root).

## Companion documents

- [autocontext-cli.md](./autocontext-cli.md) — the `autocontext` CLI binary
  (this doc's third client).
