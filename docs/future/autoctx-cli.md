# Plan: `autoctx` CLI (thin engine client, debug & scripting surface)

## Motivation

`autoctx` is the **third client** of `autocontext-engine` (alongside
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
autoctx --version
autoctx ps [--json]
autoctx config get [--workspace <path>] [--json]
autoctx config toggle <file> [<ruleId>] [--workspace <path>]
autoctx instructions list [--workspace <path>] [--json]
autoctx instructions get <name> [--raw] [--workspace <path>]
autoctx instructions search <query> [--workspace <path>] [--json]
autoctx instructions toggle <name> [<ruleId>] [--workspace <path>]
autoctx instructions watch [--workspace <path>] [--json]
autoctx workspace detect [<path>] [--json]
autoctx workspace info [--workspace <path>] [--json]
autoctx mcp list [--workspace <path>] [--json]
autoctx mcp invoke <tool> --args <json> [--workspace <path>]
autoctx route "<prompt>" [--workspace <path>] [--json]
autoctx engine status [--instance-id <uuid>] [--workspace <path>] [--json]
autoctx engine logs   [--follow] [--worker <id>] [--since <iso>] [--last-n <n>]
                      [--instance-id <uuid>] [--workspace <path>]
```

Global engine-spawn pass-through switches — accepted before any
verb, applied **only** when the CLI cold-spawns an engine for the
resolved workspace, ignored when attaching by `--instance-id` to an
already-running engine: `--idle-timeout <seconds>`,
`--retention <duration>`, `--logging <verbosity>`. See
[autocontext-engine.md → Engine options (CLI surface)](./autocontext-engine.md#engine-options-cli-surface)
for the semantics of each; the CLI forwards the values verbatim and
never interprets them. The CLI also always mints a fresh
`--instance-id` (UUIDv4, one per invocation) and a fixed
`--instance-label "autoctx (vX.Y.Z); engine (vX.Y.Z)"` when it
spawns — both are launcher-side concerns, not user-tunable.

What each verb does, on the wire:

- **`ps`** — list every live engine on the machine by reading the
  shared liveness registry at
  `…\autocontext\engine-metadata.json` (Windows
  `%LOCALAPPDATA%`, POSIX `$XDG_CACHE_HOME` or `~/.cache`)
  **directly**. The CLI neither spawns nor dials an engine for
  this verb; it opens the file, pid-checks every row (`pid`
  exists AND `Process.StartTime` ≈ `processStartTimeUtc` within
  ~1 s tolerance, to defeat pid recycling), and renders only rows
  that pass. Columns include `workspaceHash`, `instanceId`,
  `instanceLabel`, `pid`, `engineVersion`, `startedAt`, and
  `retention`; `--json` emits the registry row payload verbatim.
  A corrupt or missing registry is reported as an empty list with
  a stderr warning; the next engine start re-seeds the file. This
  is the same registry the engine's own housekeeping sweep reads
  (see [autocontext-engine.md → Housekeeping](./autocontext-engine.md#housekeeping));
  `ps` is the read-only observability counterpart.
- **`config get`** → `Config.Get` over the engine's `rpc` pipe;
  pretty-print by default, `--json` for raw JSON.
- **`config toggle <file> [<ruleId>]`** → `Config.ToggleFile`
  (file form) or `Config.ToggleRule` (rule form when `<ruleId>` is
  supplied). Writes go through the engine, never directly to
  `.autocontext.json`.
- **`instructions list`** → `Instructions.List`. Identity, override
  source, disabled flag, always-attached flag. Sections payload
  omitted by default; `--json` emits the wire row verbatim.
- **`instructions get <name>`** → `Instructions.Get(name)`
  (projected — `[INSTxxxx]` tags stripped, disabled rules
  filtered, override preferred over bundled) by default; `--raw`
  uses `Instructions.GetRaw(name)` for the unmodified source. The
  response is a discriminated envelope (`ok` / `disabled` /
  `not-found`); the CLI surfaces each distinctly — body on `ok`
  (exit `0`); explicit "muted by `.autocontext.json`" on `disabled`
  (exit `0` — existence is the answer); explicit "no such
  instruction file" on `not-found` (exit `1`).
- **`instructions search <query>`** → `Instructions.SearchContent`.
  Ranked matches with section anchors and excerpts; disabled files
  are excluded by default. `--include-disabled` flips this for
  export / audit scenarios.
- **`instructions toggle <name> [<ruleId>]`** → same RPCs as
  `config toggle`; the verb exists for users thinking in
  instruction-name terms.
- **`instructions watch`** → `Instructions.Subscribe` on `rpc` plus
  `Engine.Lifecycle.Subscribe` on `events`. Streams JSONL on stdout
  (`{event, name, ...}` per change); a `reloaded` lifecycle event
  resubscribes against the new generation, and a `shuttingDown`
  event exits cleanly with `130` (the SIGINT exit code) rather than
  treating the impending disconnect as an error.
- **`workspace detect [<path>]`** → resolves `<path>` (or CWD) to a
  normalised workspace path, cold-spawns an engine for that
  workspace under a freshly-minted instance UUID (see *Cold-start
  protocol*), and reads `Workspace.Detect` from it. Engines are
  (workspace, launcher-instance)-scoped — the CLI is its own
  launcher per invocation — so there is no "detect arbitrary path
  against an existing engine" mode. See
  [autocontext-engine.md → Process scoping](./autocontext-engine.md#process-scoping-one-engine-per-launcher-instance-per-workspace).
- **`workspace info`** → `Workspace.Info`. Engine-process metadata
  (resolved workspace path, engine version, generation counter,
  idle-timeout state) for the engine the CLI just dialled.
- **`mcp list`** → `McpTools.List`. The catalogue of MCP tools the
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
  `engine-metadata.json` and selects the unique live engine for the
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

What is **deliberately not** in the CLI:

- **No `service` subcommand.** The original design surfaced
  `autoctx service mcps://...` and `autoctx service worker://...` to
  launch processes. With the engine model both vanish: MCP hosts
  launch `autocontext-engine` directly (it is the MCP server, under
  `--mcp-server with-stdio`), and the engine launches workers
  directly (they are `AutoContext.Worker.DotNet[.exe]` etc., already
  separate binaries). The CLI never wears the launcher hat for
  those.
- **No engine-control verbs.** Running the engine is a separate
  binary (`autocontext-engine`); the CLI cold-spawns it on demand
  for verbs that need it and the engine idle-shuts itself. There is
  no `autoctx engine start` / `stop` / `restart` / `daemon`. The
  `engine status` / `engine logs` verbs are read-only observability
  surfaces dialing the engine's `health` and `logs` pipes;
  foreground engine debugging is `autocontext-engine --workspace
  <path> --instance-id <uuid>` invoked directly.
- **No `--clean` / housekeeping verb.** Per-instance subtree
  cleanup is the engine's own job, run on every engine startup and
  graceful shutdown against the shared liveness registry (see
  [autocontext-engine.md → Housekeeping](./autocontext-engine.md#housekeeping));
  the design refuses to rely on a CLI subcommand the user has to
  remember to run. `autoctx ps` is the observability counterpart —
  read-only over the same registry, never destructive.
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
- **Streams.** Output to stdout, logs and progress to stderr. JSON
  output (`--json`) is one object per line on stdout; pretty output
  is human-formatted on stdout. Never mix.
- **Colour.** Auto-detected from terminal capability; respect
  `NO_COLOR` (no colour) and `FORCE_COLOR` (force colour) per the
  conventional environment-variable contract.
- **Versioning.** `autoctx --version` prints the package version
  (sourced from `version.json` via
  `AssemblyInformationalVersionAttribute`); the version is
  RID-independent. Wire-protocol version is checked at handshake
  time, not advertised by `--version`.

## Cold-start protocol (find-or-spawn)

The CLI is its own launcher instance — one invocation = one
launcher = one engine. Every verb that talks to the engine follows
the same flow, dialling only the pipes that verb needs:

1. **Resolve the workspace path.** Either `--workspace <path>` or
   the CWD; normalise (resolve symlinks, lowercase on Windows)
   before hashing.
2. **Mint or recover the instance UUID.** The CLI mints one UUIDv4
   per invocation. Most verbs use that freshly-minted UUID and
   cold-spawn the engine themselves; `engine status`, `engine logs`,
   and any `--instance-id`-tagged invocation skip minting and use
   the UUID supplied on the command line (or resolved from the
   shared registry under each verb's single-live-engine rule).
3. **Compute the four pipe names.** Each engine instance binds four
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
   handshake required. `ps` dials no pipe at all (it reads
   `engine-metadata.json` directly). The engine binds all four
   pipes before accepting on any of them, so dial-only-what-you-need
   is safe even on cold start.
5. **Try to connect.** No pre-flight existence check (Unix-socket
   existence tests are unreliable cross-platform). One try per
   needed pipe, short timeout, treated as "engine absent" on
   failure.
6. **On failure, spawn `autocontext-engine` detached.** Resolved
   via `AppContext.BaseDirectory` from the CLI binary's location
   to the nested side-car path (`./engine/autocontext-engine[.exe]`
   relative to `autoctx[.exe]`; see *Distribution*), with no PATH
   dependency, launched with the mandatory `--workspace
   <normalisedPath>` and `--instance-id <uuid>` switches plus the
   `--instance-label "autoctx (vX.Y.Z); engine (vX.Y.Z)"`
   convention label (see
   [autocontext-engine.md → Engine options](./autocontext-engine.md#engine-options-cli-surface)).
   Optional pass-through switches (`--idle-timeout`, `--retention`,
   `--logging`) are forwarded from the CLI's global-switch surface.
   The CLI uses `Process.Start` with `UseShellExecute = false` and
   redirected/null stdio; the spawned engine is not a child in any
   meaningful sense — no parent-child IPC, no inherited handles.
   The engine and the CLI communicate only over the workspace
   pipes. Verbs that "never spawn" (`ps`, `engine status`, `engine
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
Two short-lived `autoctx` invocations against the same workspace
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
resubscription against the new generation, and a `shuttingDown`
event is the CLI's cue to exit cleanly with the same exit code as
a normal Ctrl-C (`130`) rather than treating the impending
disconnect as an error.

## Distribution

`autoctx` ships in the same per-RID layout as the engine, with the
engine bundle nested as a side-car under the CLI bundle so a cold
`autoctx` invocation can resolve and spawn its engine without a
PATH dependency. Per-RID layout (the inner engine tree is re-stated
from
[autocontext-engine.md#distribution](./autocontext-engine.md#distribution)
so this doc is self-contained):

```
cli/<rid>/
  autoctx[.exe]                          # this binary
  <framework dlls / runtime files>       # self-contained .NET runtime for the CLI
  engine/                                # embedded engine bundle — same layout as
                                         # autocontext-engine.md § Distribution
    autocontext-engine[.exe]
    <framework dlls / runtime files>     # self-contained .NET runtime for the engine
    Instructions/                        # curated corpus (engine-consumed)
    Resources/                           # build-generated read-only manifests
    Workers/                             # per-worker subdirs (engine-spawned)
```

Supported RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-x64`, `osx-arm64`. Bundle locations (the same per-RID tree
shows up in every host that ships the CLI):

- `<vsix>/cli/<rid>/...` for the VS Code extension.
- `<plugin-root>/cli/<rid>/...` for the Anthropic plugin.
- A standalone GitHub release publishes the same per-RID artefact
  for users who want `autoctx` on their PATH.

The CLI itself does not consume the bundled `engine/` side-cars at
runtime — the engine does. The CLI bundle embeds the engine's full
per-RID tree only so a cold `autoctx` invocation can resolve and
spawn its sibling engine without a PATH dependency. The CLI bundle
is distinct from the engine-only bundle (`engine/<rid>/...`) the
VS Code extension also ships for its own engine spawning; the two
trees are duplicates of the same per-RID artefact, sized for the
launcher that resolves them.

## Sharing principle (overarching)

The CLI is one of three engine clients; sharing happens at the
**wire-protocol** level, not at the source-code level. The CLI
project is itself split in two so third-party .NET code can embed
the engine client without depending on the verb-parsing or
output-formatting layer that the `autoctx[.exe]` binary adds on
top.

- **Two .NET libraries, one binary.**
  - `AutoContext.Engine.Client` — the embeddable .NET wire client.
    Owns the four-pipe dial state machine (`rpc` / `events` /
    `health` / `logs`), the cold-start-or-attach resolver, the
    typed RPC client surface (one method per engine RPC), the
    discriminated envelopes every state-bearing read returns, and
    the subscription plumbing for `*.Subscribe` channels. No
    `System.CommandLine`, no console I/O, no host-specific
    assumptions — this is the .NET analogue of the TS
    `AutoctxClient`. Third-party .NET code (custom integrations,
    automated regression harnesses, future JetBrains / Rider
    plugins, an `AutoContext.VsCode.Cs` rewrite) takes a dependency
    on this library without taking a dependency on the CLI.
  - `AutoContext.Cli` — the verb-parsing, output-formatting,
    JSON-rendering layer the `autoctx[.exe]` binary composes over
    `AutoContext.Engine.Client`. The binary's `Program.Main` calls
    `AddAutoContextCli` (see *Composition contracts*); embedders
    that want CLI behaviour in-process (a test harness driving
    every verb, a parent process exposing `autoctx` verbs through
    its own surface) do the same.
- **The TS-side `AutoctxClient`** (used by the VS Code extension
  and by Anthropic plugin `.cjs` hook scripts under whichever hook
  host runs them) speaks the same wire protocol
  `AutoContext.Engine.Client` speaks. The two are independent
  implementations of one wire contract; neither is the source of
  truth, the **engine** is.
- **Shells stay thin.** `AutoContext.Cli` contains verb parsing,
  RPC plumbing, output formatting, and the run / teardown loop —
  and nothing else. Logic that is not host-specific belongs in the
  engine. If a CLI verb starts looking like a re-implementation of
  an engine internal, the verb is wrong and the engine RPC should
  grow instead.
- **No invented cross-host seams.** This is *not* a ban on .NET DI.
  Inside both libraries use `Microsoft.Extensions.Hosting`
  (`Host.CreateApplicationBuilder`), `IHostedService` for
  long-running verbs (`instructions watch`, `engine logs --follow`),
  `IOptions<T>` from `IConfiguration`, and `ILogger<T>` for stderr
  logs exactly as the rest of the .NET solution does. New
  interfaces only appear when a *second concrete* implementation is
  being added now — not hypothetically later.

## Composition contracts

Two extension-method seams are part of the design — one per
library — and nothing else. The split mirrors the engine's split
(`AddAutoContextEngine` for the engine library, the
`autocontext-engine[.exe]` binary on top); the CLI gets the
analogous split on the client side.

- **`IHostApplicationBuilder.AddAutoContextEngineClient(Action<EngineClientOptions> configure)`**
  is `AutoContext.Engine.Client`'s single public entry point. It
  registers the four-pipe dial state machine, the cold-start /
  attach resolver, the typed RPC client surface (one method per
  engine RPC), and the lifecycle / subscription plumbing.
  `EngineClientOptions` exposes:
  - workspace path resolution (explicit path or CWD-derived);
  - launcher-identity controls — `InstanceId` override (default:
    fresh UUIDv4 per resolver instance), `InstanceLabel` template
    (default: `"autoctx (vX.Y.Z); engine (vX.Y.Z)"`);
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
  `AutoContext.Cli`, or anything verb-shaped. Tests embed it the
  same way the production binary does.
- **`IHostApplicationBuilder.AddAutoContextCli(Action<CliOptions> configure)`**
  is `AutoContext.Cli`'s single public entry point. It composes on
  top of `AddAutoContextEngineClient` and adds verb parsing
  (`System.CommandLine`), output formatting (pretty / JSON), the
  stderr-vs-stdout discipline (see *Surface conventions*), and the
  JSONL streaming pump for long-running verbs. `CliOptions` exposes
  the verb-layer knobs (output target, colour override, argv source
  for tests); the underlying engine-client knobs remain reachable
  through `CliOptions.ConfigureEngineClient` so an embedder can
  drive both layers from one call site.

Both seams live under the `AutoContext` namespace, regardless of
the lowercase `autoctx[.exe]` binary name. Embedders that only need
to talk to the engine call `AddAutoContextEngineClient`; embedders
that want CLI behaviour in-process call `AddAutoContextCli`; the
production `autoctx[.exe]` binary's `Program.Main` calls
`AddAutoContextCli` and lets the verb layer drive everything.

## Pitfalls

- **Workspace path resolution divergence.** The CLI must use the
  *exact* same normalisation (resolve symlinks, lowercase on
  Windows) that the engine uses for its pipe name. A one-character
  drift produces a different hash and the CLI talks to a different
  engine. Validator: a round-trip test that hashes a known path on
  both sides and asserts equality.
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
- **`autoctx ps` works without an engine.** The verb reads
  `engine-metadata.json` directly with a short retry loop to
  tolerate concurrent engine writers holding the file open; it
  never opens a pipe. A corrupt or missing registry is reported as
  an empty list with a stderr warning, not a failure — the next
  engine start re-seeds the file. This is deliberate: the scenario
  the engine's housekeeping is designed for (every engine crashed,
  registry left stale) needs a tool that surfaces "no engines
  alive" without itself spawning one.
- **Passive pipes (`health`, `logs`) do not keep the engine alive.**
  A forgotten `autoctx engine logs --follow` in a terminal cannot
  prevent idle shutdown, will not back-pressure any other client,
  and will see a clean EOF when the engine's idle gate fires (see
  [autocontext-engine.md → Lifecycle](./autocontext-engine.md#lifecycle)).
  Embedders writing automated log scrapers must treat EOF as a
  normal lifecycle event and reconnect under the cold-start protocol
  if they need to observe the next engine.
- **Embedders use `AddAutoContextEngineClient`, not the
  `autoctx[.exe]` binary.** Driving the engine programmatically by
  `Process.Start`-ing `autoctx[.exe]` and parsing its stdout is
  supported (the CLI's machine-readable output is contractual —
  see *Quiet-mode contract for CI*), but the in-process .NET
  embedding path is strictly cheaper: no marshalling through the
  console, typed RPC responses instead of JSON re-parse, long-lived
  subscriptions without per-invocation handshake cost. New .NET
  integrations should take a dependency on
  `AutoContext.Engine.Client` and call
  `AddAutoContextEngineClient` (see *Composition contracts*); the
  CLI binary's existence does not deprecate the library.
- **`autoctx --version` is RID-independent.** Driven by
  `AssemblyInformationalVersionAttribute` from `version.json`.
  Wire-protocol version is a *separate* integer checked in
  `Engine.Hello`; it changes on wire-format breaks, the package
  version changes on releases. Don't conflate.
- **`autoctx instructions watch` cancellation.** Long-running JSONL
  stream. Must unwind cleanly on Ctrl-C: `await foreach` with a
  forwarded `CancellationToken`, no buffer-the-world-then-emit, no
  hang on the underlying `Channel<T>` read.
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
  corpus that ships next to `autoctx` is the engine's corpus; the
  CLI sees it only via `Instructions.*` RPCs.

## Implementation phase shape

The phase-by-phase plan — ordering, deliverables, test plans,
decision rationale — lives in
`plan-autoctx-cli-implementation.md` (repo memory) alongside the
engine plan; the phases are interleaved because the CLI and the
engine must land together (the CLI can't ship without the engine,
and shipping the engine without a debug client is a regression).

Shape:

- **Skeleton.** `AutoContext.Cli` project, empty
  `AddAutoContextCli`, `autoctx --version`. Sibling of the empty
  `AutoContext.Engine` skeleton.
- **Verbs land alongside engine RPCs.** Each verb in this doc lands
  in the same release as the engine RPC it consumes, with the
  round-trip test that exercises both sides.
- **Distribution wiring.** `build.ps1 Package` produces both
  binaries in the per-RID staging dir; integration tests assert
  `autocontext-engine` resolves under `cli/<rid>/engine/` from
  `AppContext.BaseDirectory` on every supported RID.
- **Smoke tests.** Mocha-driven smoke runs invoke `autoctx
  --version`, `autoctx workspace detect`, and `autoctx
  instructions list` against a fixture workspace, asserting cold
  spawn → handshake → result → engine idle-shutdown.

## Companion documents

- [autocontext-engine.md](./autocontext-engine.md) — the engine binary the
  CLI is a client of. Wire protocol, RPC surface, lifecycle,
  distribution layout, projection ownership.
- [plan-agent-plugin-discovery-enhancements.md](./plan-agent-plugin-discovery-enhancements.md)
  — the Anthropic plugin (a sibling client of the engine).
