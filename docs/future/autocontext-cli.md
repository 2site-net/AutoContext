# Plan: `autocontext` CLI (thin engine client, debug & scripting surface)

## Motivation

`autocontext` is the **third client** of `autocontext-engine` (alongside
the VS Code extension and the Anthropic plugin), and the only one
that is neither an editor nor a hook runtime. Its job is to give
humans and CI scripts the same view the editors get, without
needing an editor host installed:

- **Standalone debugging.** Reproduce projection, override resolution,
  and config state on a workspace from a terminal, without launching
  VS Code or starting a Claude session.
- **Scripting & CI.** Inspect or toggle `.autocontext.json`, dump
  projected instruction bodies, watch state changes from a shell —
  all returning structured exit codes and machine-readable output.
- **Engine driver.** Cold-start (or attach to) the engine for a
  workspace, no editor required.

The CLI is **not** an alternate state owner. It does not project
instructions itself, does not read `.autocontext.json` directly for
display, and does not bundle its own copy of the corpus for runtime
use. Every read goes through the engine; every write is an RPC the
engine validates. See [autocontext-engine.md](./autocontext-engine.md) for
the engine's design.

## CLI surface

```
autocontext --version
autocontext config get [--workspace <path>] [--json]
autocontext config toggle <file> [<ruleId>] [--workspace <path>]
autocontext instructions list [--workspace <path>] [--json]
autocontext instructions get <name> [--raw [--source <bundled|override|active>]] [--workspace <path>]
autocontext instructions search <query> [--workspace <path>] [--json]
autocontext instructions toggle <name> [<ruleId>] [--workspace <path>]
autocontext instructions watch [--workspace <path>] [--json]
autocontext workspace detect [<path>] [--json]
autocontext workspace info [--workspace <path>] [--json]
autocontext mcp list [--workspace <path>] [--json]
autocontext mcp invoke <tool> --args <json> [--workspace <path>]
autocontext route "<prompt>" [--workspace <path>] [--json]
autocontext engine list   [--all] [--workspace <path>] [--json]
autocontext engine status [--instance-id <uuid>] [--workspace <path>] [--json]
autocontext engine logs   [--follow] [--worker <id>] [--since <iso>] [--last-n <n>]
                      [--instance-id <uuid>] [--workspace <path>]
autocontext engine stop   [--instance-id <uuid> | --all] [--workspace <path>]
                      [--grace <ms>] [--reason <text>] [--json]
```

### Verbs

One-line summary per verb. Wire-level behaviour, exit codes, and
discriminated-envelope handling are in *What each verb does, on the
wire* below.

| Verb | Engine RPC / pipe | Purpose |
|---|---|---|
| `--version` | — | Print the package version and exit. |
| `config get` | `Config.Get` | Print the current `.autocontext.json` snapshot (file toggles, rule toggles, disabled MCP tools / tasks) as the engine sees it. |
| `config toggle <file> [<ruleId>]` | `Config.ToggleFile` / `Config.ToggleRule` | Mute or un-mute one instruction file (file form) or one rule inside a file (rule form). The engine owns the write; the CLI never edits `.autocontext.json` directly. |
| `instructions list` | `Instructions.List` | List every instruction file the engine knows about — identity, override source, disabled flag, always-attached flag. |
| `instructions get <name>` | `Instructions.Get` / `Instructions.GetRaw` | Print one instruction file's body. Default is the projected view the agents see (tags stripped, disabled rules filtered, override preferred); `--raw` returns the on-disk bytes from the source selected by `--source`. |
| `instructions search <query>` | `Instructions.SearchContent` | Full-text search across the projected instruction corpus; returns ranked matches with section anchors and excerpts. |
| `instructions toggle <name> [<ruleId>]` | `Config.ToggleFile` / `Config.ToggleRule` | Same as `config toggle`, but keyed by instruction name instead of file path. |
| `instructions watch` | `Instructions.Subscribe` + `Engine.Lifecycle.Subscribe` | Stream change envelopes as JSONL on stdout until Ctrl-C — each envelope carries the engine's current `revision` and a `changes[]` list. |
| `workspace detect [<path>]` | `Workspace.Detect` | Resolve a path (or CWD) to a normalised workspace and print the engine's detection result (workspace kind, root, indicators). |
| `workspace info` | `Workspace.Info` | Print engine-process metadata for the resolved workspace — engine version, `(instanceId, revision)` pair, idle-timeout state. |
| `mcp list` | `McpTools.List` | List the MCP tools the engine would advertise to an MCP host, filtered by the same disabled-tools / disabled-tasks state. |
| `mcp invoke <tool> --args <json>` | `McpTools.Invoke` | Invoke one MCP tool through the same handler the engine's MCP-server-only role uses for `tools/call`. |
| `route "<prompt>"` | `Discovery.RouteForPrompt` | Print the routing signal the Anthropic plugin's `UserPromptSubmit` hook consumes — matched categories, extensions, strongly-relevant tools and instruction files. |
| `engine list` | reads `engine-registry.json` directly | List engines registered in the shared liveness registry — workspace hash, instance UUID, label, pid, version, start time, retention window. Default: only entries that pass pid-check (live engines). `--all`: include stale entries (entries whose pid no longer matches `processStartTimeUtc`) marked as such. Never spawns, never dials a pipe. |
| `engine status` | dials the `health` pipe | Print the small status JSON document the resolved engine emits on its `health` pipe. Read-only; never spawns. |
| `engine logs` | dials the `logs` pipe (or `Logs.Tail*`) | Snapshot or tail the resolved engine's NDJSON log stream — engine records and worker records distinguished by the `category` field. Read-only; never spawns. |
| `engine stop` | `Engine.Shutdown` | Ask the resolved engine (or every live engine on the workspace with `--all`) to shut down gracefully. Targets daemon-role engines only — MCP-server-only engines exit on stdio EOF and are invisible to this verb. |

### Options

The surface above mixes three categories of flags — **global
engine-spawn pass-throughs** that apply only when the CLI
cold-spawns an engine, **verb-shape flags** that change how a
single verb resolves its target or formats its output, and
**per-verb filters** that scope a specific RPC's payload. They are
documented separately here so the per-verb prose below can stay
focused on wire behaviour.

#### Global engine-spawn pass-throughs

Accepted **before any verb**, applied **only when the CLI
cold-spawns an engine** for the resolved workspace, **ignored when
attaching by `--instance-id`** to an already-running engine (the
already-running engine was configured by its own launcher and its
options are fixed for that engine's lifetime). The CLI forwards
these verbatim to `autocontext-engine` and never interprets them.

| Flag | Forwards to | What it does |
|---|---|---|
| `--idle-timeout <seconds>` | `autocontext-engine --idle-timeout` | Sets the spawned engine's idle gate. Non-negative integer; `0` disables the gate and ties the engine's lifetime to explicit shutdown only. The CLI never passes `0` itself (it owns no long-lived launcher and wants its spawned engines to clean up after the verb completes), but accepts the value on the CLI surface for testing scenarios where the operator wants to keep the engine alive past the verb. See [autocontext-engine.md → Engine options](./autocontext-engine.md#engine-options-cli-surface). |
| `--retention <duration>` | `autocontext-engine --retention` | Sets the spawned engine's housekeeping retention window for its own per-instance log subtree. Duration string (`<n>{s\|m\|h\|d}`; `0` = sweep immediately). |
| `--logging <verbosity>` | `autocontext-engine --logging` | Sets the spawned engine's log verbosity and rotation thresholds. `normal` rotates at 1,000 lines or 5 MB; `debug` rotates at 5,000 lines or 25 MB. |

The CLI also always mints a fresh `--instance-id` (UUIDv4, one per
invocation) and a fixed
`--instance-label "autocontext (vX.Y.Z); engine (vX.Y.Z)"` for
every engine it spawns. Both are launcher-side concerns and not
user-tunable from the CLI surface; supplying `--instance-id <uuid>`
on a verb is an **attach** instruction (dial that already-running
engine), not a spawn instruction.

#### Verb-shape flags

These appear on multiple verbs and behave the same way every time.

| Flag | Where it appears | What it does |
|---|---|---|
| `--workspace <path>` | every verb except `--version` | Selects the workspace the verb resolves against. Absent ⇒ CWD. The CLI normalises the path (uppercase on Windows, trim trailing separators; **no** symlink resolution — see *Path normalisation* in `autocontext-engine.md` § P4) **identically** to the engine's endpoint hash, so the dialled engine is the one the engine actually bound. `engine list` is the one exception to the CWD default: absent `--workspace` lists every workspace on the machine, and an explicit `--workspace <path>` filters the registry to entries whose `workspaceHash` matches that path. |
| `--json` | every read-shaped verb (`config get`, `instructions list\|search\|watch`, `workspace detect\|info`, `mcp list`, `route`, `engine list`, `engine status`) and `engine stop` | Emits the wire payload verbatim on stdout, one JSON object per line for streaming verbs. The default is human-formatted pretty output; `--json` is the machine-readable contract for CI. Logs and progress always stay on stderr regardless of mode (see *Surface conventions*). |
| `--instance-id <uuid>` | `engine status`, `engine logs`, `engine stop` | Targets a specific live engine by its launcher-minted UUID. Absent ⇒ the verb consults `engine-registry.json` and selects the unique live engine for the resolved workspace; ambiguous cases (two launchers open against the same workspace) fail with an error listing every candidate's `instanceId` and `instanceLabel`. These verbs **never cold-spawn**, so an unresolvable `--instance-id` is reported as engine-absent. |

#### Per-verb filters

These scope a single verb's payload or behaviour and have no
cross-verb meaning.

| Verb | Flag | What it does |
|---|---|---|
| `instructions get` | `--raw` | Switches the verb from `Instructions.Get` (projected — tags stripped, disabled rules filtered, override preferred) to `Instructions.GetRaw` (unmodified on-disk bytes; YAML frontmatter and `[INSTxxxx]` tags intact). |
| `instructions get` | `--source <bundled\|override\|active>` | Only meaningful with `--raw`. Selects which on-disk file the bytes come from: `active` (default — override if one exists, else bundled), `bundled` (the bundled file even when an override exists), `override` (the override file or `not-found`). |
| `instructions search` | `--include-disabled` | Includes files whose `.autocontext.json` entry is muted in the result set. Default excludes them (audit / export is the only motivating use case). |
| `config toggle` / `instructions toggle` | `<ruleId>` (positional) | Selects rule-form toggle (`Config.ToggleRule`) instead of file-form toggle (`Config.ToggleFile`). Absent ⇒ toggles the file as a whole. |
| `mcp invoke` | `--args <json>` | The JSON-encoded `arguments` object forwarded to `McpTools.Invoke`'s `tools/call`-equivalent payload. The CLI validates only that the value is well-formed JSON; the engine validates against the tool's schema. |
| `engine logs` | `--follow` | Switches from a bounded snapshot (`Logs.GetEngine` / `Logs.GetWorker`) to a server-streaming tail (the `logs` pipe or `Logs.Tail*` RPC). Without it the verb returns the most recent records and exits. |
| `engine logs` | `--worker <id>` | Reads the per-worker log file instead of `engine.log`. Errors out if the id is unknown to the resolved engine. |
| `engine logs` | `--since <iso>` | Filters snapshot or stream output to records at or after the given ISO-8601 timestamp. |
| `engine logs` | `--last-n <n>` | Caps the snapshot to the most recent `n` records (snapshot only — `--follow` ignores this). |
| `engine list` | `--all` | Include entries from `engine-registry.json` whose pid-check fails (stale leftovers from crashed engines that did not get to remove their entry). Default omits them. Stale entries are flagged in the rendered table and carry `"state": "stale"` in `--json` output; live entries carry `"state": "live"`. |
| `engine stop` | `--all` | Mutually exclusive with `--instance-id`. Broadcasts `Engine.Shutdown` to **every** live engine for the resolved workspace, one RPC per engine, issued in parallel; each engine drains independently. |
| `engine stop` | `--grace <ms>` | Forwarded verbatim to `Engine.Shutdown.opts.grace`. Caps how long the engine waits for in-flight `rpc` handlers to complete before closing pipes; default `2000`, engine-side hard cap `30000`. |
| `engine stop` | `--reason <text>` | Forwarded verbatim to `Engine.Shutdown.opts.reason`. Opaque postmortem string (≤ 200 printable-ASCII chars); appears on the engine's final `engine.lifecycle` log line and nowhere else. |

What each verb does, on the wire:

- **`engine list`** — list engines registered in the shared
  liveness registry at `…\autocontext\engine-registry.json`
  (Windows `%LOCALAPPDATA%`, POSIX `$XDG_CACHE_HOME` or
  `~/.cache`) by reading the file **directly**. The CLI neither
  spawns nor dials an engine for this verb; it opens the file and
  pid-checks every entry (`pid` exists AND `Process.StartTime` ≈
  `processStartTimeUtc` within ~1 s tolerance, to defeat pid
  recycling). Default behaviour renders only entries that pass
  pid-check (live engines); `--all` additionally renders entries
  that fail pid-check (stale leftovers from crashed engines whose
  housekeeping never ran) and flags them as such. Default scope
  is machine-wide (every workspace); `--workspace <path>` filters
  the listing to entries whose `workspaceHash` matches the
  normalised path — unlike every other verb, `engine list` does
  **not** default to CWD, because a listing verb whose default
  hides most of what it could show is a footgun. Columns include
  `state` (`live` / `stale`, the latter only ever appearing under
  `--all`), `workspaceHash`, `instanceId`, `instanceLabel`,
  `pid`, `engineVersion`, `startedAt`, and `retention`; `--json`
  emits each entry as one JSON object on stdout with the same
  `state` discriminator. A corrupt or missing registry is reported
  as an empty list with a stderr warning; the next engine start
  re-seeds the file. This is the same registry the engine's own
  housekeeping sweep reads (see
  [autocontext-engine.md → Housekeeping](./autocontext-engine.md#housekeeping));
  `engine list` is the read-only observability counterpart.
  Like `engine status` / `engine logs` / `engine stop`, it
  **never cold-spawns** — reading the registry to list engines
  has no sensible engine-spawn fallback.
- **`config get`** → `Config.Get` over the engine's `rpc` pipe;
  pretty-print by default, `--json` for raw JSON.
- **`config toggle <file> [<ruleId>]`** → `Config.ToggleFile`
  (file form) or `Config.ToggleRule` (rule form when `<ruleId>` is
  supplied). Writes go through the engine, never directly to
  `.autocontext.json`. The RPC returns once the dialled engine has
  flushed the new state to disk and published the resulting
  snapshot to its own subscribers; peer engines on the same
  workspace observe the change through their `FileSystemWatcher`
  and fan it out to their own clients within FS-watcher latency
  (see
  [autocontext-engine.md → Reload coalescing](./autocontext-engine.md#reload-coalescing-debounce-and-batch)).
  When multiple clients of the *same* engine (the extension's
  tree view, a hook, a future bulk-toggle verb) issue
  `Config.Toggle*` RPCs within tens of milliseconds, the engine
  coalesces them server-side into one on-disk write and one
  fan-out envelope — each RPC still returns success individually,
  but subscribers on `instructions watch` see the group arrive
  as one batch. **Two separate `autocontext config toggle`
  invocations do not batch** with each other — each invocation
  mints its own instance UUID and spawns its own engine (see
  *Cold-start protocol*), so there is no shared writer to
  coalesce on. The batching property is a property of one
  engine's writer, not of the CLI binary.
- **`instructions list`** → `Instructions.List`. Identity, override
  source, disabled flag, always-attached flag. Sections payload
  omitted by default; `--json` emits the wire row verbatim.
- **`instructions get <name>`** → `Instructions.Get(name)` by
  default (projected — `[INSTxxxx]` tags stripped, disabled rules
  filtered, override preferred over bundled). The response is a
  three-arm discriminated envelope (`ok` / `disabled` /
  `not-found`); the CLI surfaces each distinctly — body on `ok`
  (exit `0`); explicit "muted by `.autocontext.json`" on `disabled`
  (exit `0` — existence is the answer); explicit "no such
  instruction file" on `not-found` (exit `1`).

  With `--raw` the verb calls `Instructions.GetRaw(name, { source })`
  instead — unmodified bytes of the on-disk markdown file (YAML
  frontmatter intact, `[INSTxxxx]` tags intact, no disabled-rule
  filter). The `--source` flag selects which on-disk file the
  bytes come from (see
  [autocontext-engine.md → Instructions.GetRaw](./autocontext-engine.md#rpc-surface-initial)):
  - `active` (default) — override if one exists, else bundled.
  - `bundled` — the bundled file even when an override exists.
  - `override` — the override file or `not-found`.

  The `GetRaw` response is a two-arm envelope (`ok` / `not-found`)
  — there is no `disabled` branch, because disabled state is
  irrelevant to a source-file read. Exit codes match the `Get`
  path (`0` on `ok`, `1` on `not-found`).
- **`instructions search <query>`** → `Instructions.SearchContent`.
  Ranked matches with section anchors and excerpts; disabled files
  are excluded by default. `--include-disabled` flips this for
  export / audit scenarios.
- **`instructions toggle <name> [<ruleId>]`** → same RPCs as
  `config toggle`; the verb exists for users thinking in
  instruction-name terms.
- **`instructions watch`** → `Instructions.Subscribe` on `rpc` plus
  `Engine.Lifecycle.Subscribe` on `events`. Streams JSONL on stdout,
  one envelope per line: each envelope carries the engine's current
  `revision` plus a `changes[]` array listing every mutation in
  the batch (writer-mutex order, **not** a temporal claim — see
  [autocontext-engine.md → Reload coalescing](./autocontext-engine.md#reload-coalescing-debounce-and-batch)).
  Clients that need per-change handling iterate `changes[]`;
  clients that only need a "something changed" signal can read the
  `revision` field. A `reloaded` lifecycle event resubscribes
  against the new revision, and a `shutting-down` event exits
  cleanly with `130` (the SIGINT exit code) rather than treating
  the impending disconnect as an error.
- **`workspace detect [<path>]`** → resolves `<path>` (or CWD) to a
  normalised workspace path, cold-spawns an engine for that
  workspace under a freshly-minted instance UUID (see *Cold-start
  protocol*), and reads `Workspace.Detect` from it. Engines are
  (workspace, launcher-instance)-scoped — the CLI is its own
  launcher per invocation — so there is no "detect arbitrary path
  against an existing engine" mode. See
  [autocontext-engine.md → Process scoping](./autocontext-engine.md#process-scoping-one-engine-per-launcher-instance-per-workspace).
- **`workspace info`** → `Workspace.Info`. Engine-process metadata
  (resolved workspace path, engine version,
  `(instanceId, revision)` pair, idle-timeout state) for the
  engine the CLI just dialled.
- **`mcp list`** → `McpTools.List`. The catalog of MCP tools the
  engine would advertise to an MCP client, filtered by the same
  `disabledTools` / `disabledTasks` state the engine applies on its
  MCP/stdio facade.
- **`mcp invoke <tool> --args <json>`** → `McpTools.Invoke`. The
  pipe-RPC counterpart of MCP's `tools/call`, terminating at the
  same handler the engine's MCP/stdio facade uses. The discriminated
  response (`ok` / `tool-error` / `schema-error` / `disabled` /
  `not-found`) is surfaced distinctly: body content on `ok`
  (exit `0`); body content with non-zero exit on `tool-error`;
  structured validation errors on `schema-error` (exit `2`);
  identity-only messages on `disabled` / `not-found` (exit `0` /
  `1` respectively). SIGINT cancellation propagates through the
  pipe-RPC framing's per-request token.
- **`route "<prompt>"`** → `Discovery.RouteForPrompt`. The same
  signal the Anthropic plugin's `UserPromptSubmit` hook consumes,
  exposed as a CLI verb for repro and tuning. Output names the
  matched categories, matched extensions, strongly-relevant MCP
  tools, and strongly-relevant instruction files.
- **`engine status`** dials the `health` pipe (no `Engine.Hello`
  required) of the engine identified by `--instance-id <uuid>` for
  the resolved workspace and prints the small status JSON document
  the pipe emits. Without `--instance-id` the CLI reads
  `engine-registry.json` and selects the unique live engine for the
  resolved workspace; ambiguous cases (multiple live engines on one
  workspace — normal when two launchers are open against it) fail
  with an error listing the candidates by `instanceId` and
  `instanceLabel`. `engine status` **never spawns** — its job is
  to observe, not to bring an engine to life; absence of any live
  engine for the workspace is reported as such, with exit `1`.
- **`engine logs`** dials the `logs` pipe (no `Engine.Hello`
  required) under the same `--instance-id` / single-live-engine
  resolution rules `engine status` uses. Without `--follow` the
  verb requests a bounded snapshot via `Logs.GetEngine` (or
  `Logs.GetWorker` when `--worker <id>` is given); with `--follow`
  it server-streams the `logs` pipe (or the corresponding
  `Logs.Tail*` RPC). `--since <iso>` filters by timestamp,
  `--last-n <n>` caps the snapshot, `--worker <id>` picks the
  per-worker file instead of `engine.log` and exits with a clear
  error if the id is unknown to the resolved engine. Records are
  emitted as NDJSON on stdout with the canonical envelope
  (`{ timestamp, category, level, eventId?, message, properties?, exception? }`,
  see [autocontext-engine.md → Log categories](./autocontext-engine.md#log-categories));
  there is no pretty-print mode — `logs` is machine-readable by
  design.
- **`engine stop`** → `Engine.Shutdown` over the engine's `rpc`
  pipe. Same `--instance-id` / single-live-engine resolution as
  `engine status` and `engine logs`; `--all` broadcasts the RPC
  to every live engine for the resolved workspace (one RPC per
  engine, issued in parallel, each engine drains independently).
  **Targets the daemon role only.** MCP-server-only engines
  (`autocontext-engine --mcp-server with-stdio`) do not bind
  pipes, do not write entries to `engine-registry.json`, and are
  therefore invisible to this verb — they have no `rpc` endpoint
  to dial and no registry presence to enumerate. They exit on
  stdio EOF when their MCP host disconnects; stopping one means
  asking the host to disconnect, which is out of scope for the
  CLI.
  `--grace <ms>` forwards verbatim to `Engine.Shutdown.opts.grace`
  (default 2,000, hard-capped 30,000 by the engine); `--reason
  <text>` forwards verbatim to `opts.reason` for postmortem log
  reading. The verb **never spawns** — stopping an engine that
  is not running is a successful no-op, not a cold-spawn-then-stop
  contradiction; absence is reported with exit `0` and a stderr
  note. Exit `0` once the dialled engine(s) acknowledge
  `{ accepted: true }`; exit `1` only if the RPC itself fails
  (transport error, version mismatch, refused). The verb does
  **not** wait for the engine to actually exit — acknowledgement
  means the shutdown sequence has started, and the engine's own
  housekeeping covers the rest; pair with
  [`autocontext engine list`](#what-each-verb-does-on-the-wire) or a brief
  poll on `engine status` if a script needs to observe the exit.
  See
  [autocontext-engine.md → RPC surface (initial)](./autocontext-engine.md#rpc-surface-initial)
  for the `Engine.Shutdown` contract — in particular, authorization
  is pipe-presence (any client with the right `<instanceId>` may
  call), and concurrent invocations idempotently ride the same
  drain.

What is **deliberately not** in the CLI:

- **No `service` subcommand.** The original design surfaced
  `autocontext service mcps://...` and `autocontext service worker://...` to
  launch processes. With the engine model both vanish: MCP hosts
  launch `autocontext-engine` directly (it is the MCP server, under
  `--mcp-server with-stdio`), and the engine launches workers
  directly (they are `AutoContext.Worker.DotNet[.exe]` etc., already
  separate binaries). The CLI never wears the launcher hat for
  those.
- **No engine `start` / `restart` / `daemon` verbs.** Running the
  engine is a separate binary (`autocontext-engine`); the CLI
  cold-spawns it on demand for verbs that need it and the engine
  idle-shuts itself by default. There is no `autocontext engine
  start` (foreground engine debugging is `autocontext-engine
  --workspace <path> --instance-id <uuid>` invoked directly; long
  -lived host launchers spawn their own engine with
  `--idle-timeout 0` and own the lifecycle), no `engine restart`
  (a stop-then-spawn dance the CLI refuses to wear; combine
  `engine stop` with a follow-up verb that needs an engine if
  that is what you really want), and no `engine daemon`
  (workspace-scoping forbids machine-wide daemons — see
  [autocontext-engine.md → Process scoping](./autocontext-engine.md#process-scoping-one-engine-per-launcher-instance-per-workspace)).
  `engine status` and `engine logs` are read-only observability
  surfaces dialling the engine's `health` and `logs` pipes;
  `engine stop` is the one lifecycle-affecting verb in the
  `engine` namespace, and it only ever brings engines *down*,
  never up.
- **No `--clean` / housekeeping verb.** Per-instance subtree
  cleanup is the engine's own job, run on every engine startup and
  graceful shutdown against the shared liveness registry (see
  [autocontext-engine.md → Housekeeping](./autocontext-engine.md#housekeeping));
  the design refuses to rely on a CLI subcommand the user has to
  remember to run. `autocontext engine list` is the observability
  counterpart — read-only over the same registry, never
  destructive.
- **No in-process projection.** The CLI never re-implements
  `InstructionsFileBodyProjector` or reads `.autocontext.json`
  directly to compute results. If the engine is unreachable, the
  command fails with a clear error and exit code; it does not
  silently fall back to in-process logic.
- **No host-specific surfaces.** No "VS Code extension this", no
  "Anthropic plugin that". The CLI is a pure engine client.

## Surface conventions

- **Exit codes.** `0` success; `1` runtime failure (invalid
  workspace, RPC error); `2` usage error (unknown verb, bad arg);
  `64` (`EX_USAGE`) for parse-time argument validation; `69`
  (`EX_UNAVAILABLE`) when the engine is reachable but rejects
  `Engine.Hello` (protocol-version mismatch); `130` for SIGINT.
- **Signal handling.** `Console.CancelKeyPress` and
  `AppDomain.ProcessExit` build the root `CancellationToken` passed
  to every async operation; the CLI never spawns the engine and
  blocks on it (the engine spawn is `start /b`-style detached, see
  *Cold-start protocol*), so SIGINT only stops the in-flight RPC,
  not the engine.
- **Async end-to-end.** Every verb runs on the .NET async stack
  from `Program.Main` (returning `Task<int>`) down to the pipe
  read/write — no `.Result`, no `.Wait()`, no
  `GetAwaiter().GetResult()` anywhere on the request path. The
  CLI mirrors the engine's P8 (see
  [autocontext-engine.md → P8](./autocontext-engine.md#p8-async-io-end-to-end-no-sync-over-async-no-blocking-on-hot-paths)).
  Streaming verbs (`instructions watch`, `engine logs --follow`,
  any `*.Subscribe` consumer) drain the wire stream with
  `await foreach` over an `IAsyncEnumerable<T>` of envelopes, emit
  each record as soon as it arrives, and unwind cleanly on
  cancellation — no "buffer the world, then print", no hangs on
  the underlying channel read. Snapshot verbs (`config get`,
  `instructions list`, `workspace info`, …) issue one async RPC
  and exit; the dial-only-what-you-need rule keeps the connect
  cost proportional to the verb. Retry and backoff inside the
  cold-start protocol use `Task.Delay(..., cancellationToken)` so
  a Ctrl-C during a cold spawn returns immediately with exit
  `130` instead of riding out the cold-connect budget.
- **Streams.** Output to stdout, logs and progress to stderr. JSON
  output (`--json`) is one object per line on stdout; pretty output
  is human-formatted on stdout. Never mix.
- **Colour.** Auto-detected from terminal capability; respect
  `NO_COLOR` (no colour) and `FORCE_COLOR` (force colour) per the
  conventional environment-variable contract.
- **Versioning.** `autocontext --version` prints the package version
  (sourced from `version.json` via
  `AssemblyInformationalVersionAttribute`); the version is
  RID-independent. Wire-protocol version is checked at handshake
  time, not advertised by `--version`.

## Cold-start protocol (find-or-spawn)

The CLI is its own launcher instance — one invocation = one
launcher = one engine. Every verb that talks to the engine follows
the same flow, dialling only the pipes that verb needs:

1. **Resolve the workspace path.** Either `--workspace <path>` or
   the CWD; normalise (uppercase on Windows, trim trailing
   separators; **no** symlink resolution — see
   `autocontext-engine.md` § P4) before hashing.
2. **Mint or recover the instance UUID.** The CLI mints one UUIDv4
   per invocation. Most verbs use that freshly-minted UUID and
   cold-spawn the engine themselves; `engine status`, `engine logs`,
   and any `--instance-id`-tagged invocation skip minting and use
   the UUID supplied on the command line (or resolved from the
   shared registry under each verb's single-live-engine rule).
3. **Compute the four endpoints.** Each engine instance binds four
   named pipes named
   `autocontext-engine:<kind>@<workspaceHash>#<instanceId>`, where
   `<kind>` ∈ {`rpc`, `events`, `health`, `logs`}, the hash is
   `sha256(normalisedWorkspacePath):0..16`, and `<instanceId>` is
   the UUIDv4 from step 2. Clients and engine agree byte-for-byte
   (see [autocontext-engine.md → Lifecycle](./autocontext-engine.md#lifecycle)).
4. **Dial only the pipes the verb needs.** Workspace-state verbs
   (`config *`, `instructions *`, `workspace *`, `mcp *`, `route`)
   dial `rpc`. Long-running watch verbs additionally dial `events`.
   `engine status` dials only `health` — passive, no handshake
   required. `engine logs` dials only `logs` — passive, no
   handshake required. `engine list` dials no pipe at all (it reads
   `engine-registry.json` directly). The engine binds all four
   pipes before accepting on any of them, so dial-only-what-you-need
   is safe even on cold start.
5. **Try to connect.** No pre-flight existence check (Unix-socket
   existence tests are unreliable cross-platform). One try per
   needed pipe, short timeout, treated as "engine absent" on
   failure.
6. **On failure, spawn `autocontext-engine` detached.** Resolved
   via `AppContext.BaseDirectory` from the CLI binary's location
   to the nested side-car path (`./engine/autocontext-engine[.exe]`
   relative to `autocontext[.exe]`; see *Distribution*), with no PATH
   dependency, launched with the mandatory `--workspace
   <normalisedPath>` and `--instance-id <uuid>` switches plus the
   `--instance-label "autocontext (vX.Y.Z); engine (vX.Y.Z)"`
   convention label (see
   [autocontext-engine.md → Engine options](./autocontext-engine.md#engine-options-cli-surface)).
   Optional pass-through switches (`--idle-timeout`, `--retention`,
   `--logging`) are forwarded from the CLI's global-switch surface.
   The CLI uses `Process.Start` with `UseShellExecute = false` and
   redirected/null stdio; the spawned engine is not a child in any
   meaningful sense — no parent-child IPC, no inherited handles.
   The engine and the CLI communicate only over the workspace
   pipes. Verbs that "never spawn" (`engine list`, `engine status`, `engine
   logs`) skip this step and report engine-absent as the result.
7. **Retry connect.** Exponential backoff against two budgets:
   sub-second warm budget, several-second cold budget. Cold-start
   for a self-contained .NET binary is hundreds of milliseconds
   plus an OS hand-off. The CLI retries against the one pipe the
   verb actually needs; sibling pipes do not require independent
   retry because the engine binds them together.
8. **`Engine.Hello` handshake** on `rpc` and `events` only
   (`health` and `logs` are payload-shape-versioned passives with
   no handshake). Single small-budget RPC; protocol version is an
   integer; mismatch refuses (CLI exits `69`). The protocol is
   exact-match and engine + clients ship versioned together; a
   refusal in production indicates a packaging mismatch and the CLI
   surfaces it rather than negotiating around it.
9. **Issue the actual RPC.** Print result, exit.

The CLI never holds the engine alive; once the verb completes it
disconnects and the engine drops back into its idle-timer state.
Two short-lived `autocontext` invocations against the same workspace
each mint their own UUID and each spawn their own engine — they do
**not** attach to each other (different `<instanceId>` = a
different engine by construction, see
[autocontext-engine.md → Process scoping](./autocontext-engine.md#process-scoping-one-engine-per-launcher-instance-per-workspace)).
This is the deliberate cost of "one launcher = one engine";
engines are cheap and idle-shut themselves within the
`--idle-timeout` window, and each engine's housekeeping sweep
clears the previous CLI invocation's leftover subtree once its
retention window elapses (see
[autocontext-engine.md → Housekeeping](./autocontext-engine.md#housekeeping)).

For long-running verbs (`instructions watch`, `engine logs
--follow`), the CLI also subscribes to `Engine.Lifecycle` on the
`events` pipe (see
[autocontext-engine.md → Authority model](./autocontext-engine.md#authority-model-engine-owns-clients-cache)):
`reloaded` events trigger a fresh `Instructions.Subscribe`
resubscription against the new revision, and a `shutting-down`
event is the CLI's cue to exit cleanly with the same exit code as
a normal Ctrl-C (`130`) rather than treating the impending
disconnect as an error.

## Distribution

`autocontext` ships in the same flat per-platform shape as the
engine, with the engine bundle nested as a side-car under the CLI
bundle so a cold `autocontext` invocation can resolve and spawn its
engine without a PATH dependency. Each shipped artefact targets one
platform — one VSIX per platform via `vsce package --target
<target>`, one plugin release per platform, one GitHub-release
tarball per RID — so the per-RID segment that exists in build
staging is **absent** from the shipped product (the inner engine
tree is re-stated from
[autocontext-engine.md → Distributed bundle layout](./autocontext-engine.md#distributed-bundle-layout)
so this doc is self-contained):

```
cli/
  autocontext[.exe]                      # this binary
  <framework dlls / runtime files>       # self-contained .NET runtime for the CLI
  engine/                                # embedded engine bundle — same shape as
                                         # autocontext-engine.md § Distributed bundle layout
    autocontext-engine[.exe]
    <framework dlls / runtime files>     # self-contained .NET runtime for the engine
    Instructions/                        # curated corpus (engine-consumed)
    Resources/                           # build-generated read-only manifests
    Workers/                             # per-worker subdirs (engine-spawned)
```

At build-output staging time the layout keeps one subtree per
supported RID (`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-x64`, `osx-arm64`); per-platform packaging picks the matching
`<rid>/` and copies its contents into `cli/` in the shipped
artefact. Bundle locations (the same flat tree shows up in every
host that ships the CLI):

- `<vsix>/cli/...` for the VS Code extension.
- `<plugin-root>/cli/...` for the Anthropic plugin.
- A standalone GitHub release publishes the same per-platform
  artefact for users who want `autocontext` on their PATH.

The CLI itself does not consume the bundled `engine/` side-car at
runtime — the engine does. The CLI bundle embeds the engine's full
tree only so a cold `autocontext` invocation can resolve and spawn
its sibling engine without a PATH dependency. The CLI bundle is
distinct from the engine-only bundle the VS Code extension also
ships for its own engine spawning; the two trees are duplicates of
the same per-platform artefact, sized for the launcher that
resolves them.

## Sharing principle (overarching)

The CLI is one of three engine clients; sharing happens at the
**wire-protocol** level, not at the source-code level. The CLI
binary is one of two host projects over the shared
`AutoContext.Client.Core` library, so third-party .NET code
can embed the engine client without taking a dependency on the
verb-parsing or output-formatting code that lives inside the
`autocontext[.exe]` binary.

- **One library, one binary** (see
  [autocontext-engine.md → Project layout](./autocontext-engine.md#project-layout)
  for the full three-library / two-binary picture).
  - `AutoContext.Client.Core` — the embeddable .NET wire
    client. Owns the four-pipe dial state machine (`rpc` /
    `events` / `health` / `logs`), the cold-start-or-attach
    resolver, the typed RPC client surface (one method per engine
    RPC), the discriminated envelopes every state-bearing read
    returns, and the subscription plumbing for `*.Subscribe`
    channels. No `System.CommandLine`, no console I/O, no
    host-specific assumptions. Third-party .NET code (custom integrations,
    automated regression harnesses, future JetBrains / Rider
    plugins, an `AutoContext.VsCode.Cs` rewrite) takes a dependency
    on this library without taking a dependency on the CLI binary.
  - `AutoContext.CommandLine` (binary) — the CLI host. `Program.Main`
    parses subcommands with `System.CommandLine`, calls
    `AddAutoContextClient` (see *Composition contracts*) to register
    the wire-client services, formats output (pretty / JSON), and
    enforces the stderr-vs-stdout discipline (see *Surface
    conventions*). Published per-RID as `autocontext[.exe]`.
    Embedders that want CLI-shaped behaviour in-process drive
    `AutoContext.Client.Core` directly through
    `AddAutoContextClient` and provide their own argv source — the
    verb-parsing layer is not factored out as a separate library
    because no second consumer is asking for it.
- **The TS-side `EngineDaemonManager`** is a different concern entirely.
  Used by the VS Code extension and by Anthropic plugin `.cjs` hook
  scripts (under whichever hook host runs them), it owns engine-daemon
  lifecycle on the TS host side (find-or-spawn, supervise, tear-down)
  and exposes typed pipe-RPC on top. The fact that it dials the same
  wire protocol `AutoContext.Client.Core` dials does **not** make
  the two parallel — they have unrelated responsibilities and unrelated
  consumers. The only thing they share is the wire contract, which
  the **engine** owns.
- **Shells stay thin.** `AutoContext.CommandLine` contains verb
  parsing, the call into `AddAutoContextClient`, output formatting,
  and the run / teardown loop — and nothing else. Logic that is
  not host-specific belongs in the engine. If a CLI verb starts
  looking like a re-implementation of an engine internal, the verb
  is wrong and the engine RPC should grow instead.
- **No invented cross-host seams.** This is *not* a ban on .NET DI.
  Both the library and the binary use
  `Microsoft.Extensions.Hosting` (`Host.CreateApplicationBuilder`),
  `IHostedService` for long-running verbs (`instructions watch`,
  `engine logs --follow`), `IOptions<T>` from `IConfiguration`, and
  `ILogger<T>` for stderr logs exactly as the rest of the .NET
  solution does. New interfaces only appear when a *second
  concrete* implementation is being added now — not hypothetically
  later.

## Composition contracts

One extension-method seam is part of the design — the same seam
the engine doc names — and nothing else. The CLI binary's
`Program.Main` parses argv with `System.CommandLine`, calls into
that seam, and dispatches verbs against the typed RPC clients the
seam registers.

- **`IHostApplicationBuilder.AddAutoContextClient(Action<ClientOptions> configure)`**
  is `AutoContext.Client.Core`'s single public entry point
  (mirror of the engine's `AddAutoContextEngine` — see
  [autocontext-engine.md → Composition contracts](./autocontext-engine.md#composition-contracts)).
  It registers the four-pipe dial state machine, the cold-start /
  attach resolver, the typed RPC client surface (one method per
  engine RPC), and the lifecycle / subscription plumbing.
  `ClientOptions` exposes:
  - workspace path resolution (explicit path or CWD-derived);
  - launcher-identity controls — `InstanceId` override (default:
    fresh UUIDv4 per resolver instance), `InstanceLabel` template
    (default: `"autocontext (vX.Y.Z); engine (vX.Y.Z)"`);
  - spawn policy — `SpawnDisabled` (connect-or-fail without
    spawning, for tests and for the `engine status` / `engine logs`
    verbs that observe but never spawn), `EngineBinaryPath`
    override (default: the nested side-car path under
    `AppContext.BaseDirectory`);
  - engine-pipe override (library-only, breaks P4 — kept off any
    binary's CLI surface intentionally);
  - pass-through engine-spawn switches (`IdleTimeout`, `Retention`,
    `Logging`) that the resolver forwards verbatim when it
    cold-spawns.

  Third-party .NET code embeds the engine client through this seam
  without taking a dependency on `System.CommandLine`,
  `AutoContext.CommandLine`, or anything verb-shaped. The CLI binary
  takes the same dependency the embedders take; what `Program.Main`
  adds on top (argv parsing, output formatting, the JSONL streaming
  pump for long-running verbs, the stderr-vs-stdout discipline
  documented under *Surface conventions*) lives inside the
  `AutoContext.CommandLine` binary project and is not factored out
  as a separate library — no second consumer is asking for it.

The seam lives under the `AutoContext` namespace, regardless of
the lowercase `autocontext[.exe]` binary name. Embedders call
`AddAutoContextClient` directly; the production `autocontext[.exe]`
binary's `Program.Main` does the same and then layers verb parsing
and output formatting on top.

## Pitfalls

- **Workspace path resolution divergence.** The CLI must use the
  *exact* same normalisation the engine uses for its endpoint —
  uppercase on Windows, trim trailing separators, **no** symlink
  resolution (see `autocontext-engine.md` § P4 for the rationale).
  A one-character drift produces a different hash and the CLI
  talks to a different engine. Validator: a round-trip test that
  hashes a known path on both sides and asserts equality.
- **Spawn-on-cold-start signal handling.** The CLI spawns
  `autocontext-engine` detached. SIGINT to the CLI must not
  propagate to the spawned engine; the engine's lifetime is
  governed by its idle timer and its other clients, not by the CLI
  invocation that happened to start it. The engine's housekeeping
  rules apply to its own leftover subtree the moment it exits (see
  [autocontext-engine.md → Housekeeping](./autocontext-engine.md#housekeeping));
  the CLI does not need to clean up after the engine it spawned.
- **`engine status` and `engine logs` never cold-spawn.** Both
  verbs observe an *existing* engine; absence of one is the answer,
  not a reason to start a new one. A CLI invocation that cold-spawned
  an engine just to read its `health` payload would pay the full
  idle-timeout cost for a one-shot status check and would never
  reach the engine the user actually wanted to observe (the editor's
  engine, identified by a different `<instanceId>`). Both verbs
  exit with a clear "no live engine for this workspace" error and
  exit `1` when no candidate exists.
- **`autocontext engine list` works without an engine.** The verb reads
  `engine-registry.json` directly with a short retry loop to
  tolerate concurrent engine writers holding the file open; it
  never opens a pipe. A corrupt or missing registry is reported as
  an empty list with a stderr warning, not a failure — the next
  engine start re-seeds the file. This is deliberate: the scenario
  the engine's housekeeping is designed for (every engine crashed,
  registry left stale) needs a tool that surfaces "no engines
  alive" without itself spawning one.
- **Passive pipes (`health`, `logs`) do not keep the engine alive.**
  A forgotten `autocontext engine logs --follow` in a terminal cannot
  prevent idle shutdown, will not back-pressure any other client,
  and will see a clean EOF when the engine's idle gate fires (see
  [autocontext-engine.md → Lifecycle](./autocontext-engine.md#lifecycle)).
  Embedders writing automated log scrapers must treat EOF as a
  normal lifecycle event and reconnect under the cold-start protocol
  if they need to observe the next engine.
- **Embedders use `AddAutoContextClient`, not the
  `autocontext[.exe]` binary.** Driving the engine programmatically by
  `Process.Start`-ing `autocontext[.exe]` and parsing its stdout is
  supported (the CLI's machine-readable output is contractual —
  see *Quiet-mode contract for CI*), but the in-process .NET
  embedding path is strictly cheaper: no marshalling through the
  console, typed RPC responses instead of JSON re-parse, long-lived
  subscriptions without per-invocation handshake cost. New .NET
  integrations should take a dependency on
  `AutoContext.Client.Core` and call
  `AddAutoContextClient` (see *Composition contracts*); the
  CLI binary's existence does not deprecate the library.
- **`autocontext --version` is RID-independent.** Driven by
  `AssemblyInformationalVersionAttribute` from `version.json`.
  Wire-protocol version is a *separate* integer checked in
  `Engine.Hello`; it changes on wire-format breaks, the package
  version changes on releases. Don't conflate.
- **`autocontext instructions watch` cancellation.** Long-running JSONL
  stream. Must unwind cleanly on Ctrl-C: `await foreach` with a
  forwarded `CancellationToken`, no buffer-the-world-then-emit, no
  hang on the underlying `Channel<T>` read.
- **Cross-engine read-after-write is not synchronous.** Two
  short-lived `autocontext` invocations against the same workspace
  each mint their own UUID and each spawn their own engine (see
  *Cold-start protocol*). A `config toggle` against engine A
  followed *immediately* by `config get` against engine B (or any
  peer engine for that workspace, including an editor's engine)
  can return the pre-toggle snapshot until B's `FileSystemWatcher`
  debounce drains — typically tens of milliseconds, longer on
  network drives or under WSL forwarding. For interactive use
  this window is invisible; for tight automated tests that need
  cross-engine read-after-write, subscribe to
  `Engine.Lifecycle.reloaded` (via `instructions watch` or the
  library's `Lifecycle.Subscribe`) on engine B and wait for the
  revision to advance past the snapshot the toggle published.
  No CLI verb promises cross-engine read-after-write today; the
  engine doc's
  [Process scoping](./autocontext-engine.md#process-scoping-one-engine-per-launcher-instance-per-workspace)
  section (the *cross-instance `.autocontext.json`* bullet) is
  the authoritative reference.
- **Quiet-mode contract for CI.** No `--quiet` flag — the contract
  is "stdout is the answer, stderr is the noise". Pipe stderr to
  `/dev/null` from a CI script and you have machine-readable
  output. Adding a `--quiet` flag would silently change that
  contract.
- **Do NOT** add a `service` subcommand. The CLI is a pure engine
  client; the engine and workers are launched by other actors (MCP
  hosts and the engine itself, respectively).
- **Do NOT** read `.autocontext.json` from the CLI directly for
  display. Every config read goes through the engine so the CLI
  always sees the same view the editors see.
- **Do NOT** bundle a runtime corpus the CLI itself consumes. The
  corpus that ships next to `autocontext` is the engine's corpus; the
  CLI sees it only via `Instructions.*` RPCs.

## Implementation phase shape

The CLI and the engine must land together — the CLI can't ship
without the engine, and shipping the engine without a debug client
is a regression — so their phases are interleaved.

Shape:

- **Skeleton.** `AutoContext.CommandLine` binary project with
  `Program.Main` calling `AddAutoContextClient` (from the empty
  `AutoContext.Client.Core` library skeleton); `autocontext
  --version` works end-to-end. Sibling of the empty
  `AutoContext.Engine` binary and `AutoContext.Engine.Core`
  library skeletons defined in
  [autocontext-engine.md → Project layout](./autocontext-engine.md#project-layout).
- **Verbs land alongside engine RPCs.** Each verb in this doc lands
  in the same release as the engine RPC it consumes, with the
  round-trip test that exercises both sides.
- **Distribution wiring.** `build.ps1 Package` produces both
  binaries in the per-RID staging layout; per-platform packaging
  flattens the matching RID subtree to `cli/` and `engine/` in the
  shipped artefact. Integration tests assert `autocontext-engine`
  resolves under `./engine/` from the CLI binary's
  `AppContext.BaseDirectory` on every supported RID.
- **Smoke tests.** Mocha-driven smoke runs invoke `autocontext
  --version`, `autocontext workspace detect`, and `autocontext
  instructions list` against a fixture workspace, asserting cold
  spawn → handshake → result → engine idle-shutdown.

## Companion documents

- [autocontext-engine.md](./autocontext-engine.md) — the engine binary the
  CLI is a client of. Wire protocol, RPC surface, lifecycle,
  distribution layout, projection ownership.
