# Plan: `autoctx` CLI (thin engine client, debug & scripting surface)

## Motivation

`autoctx` is the **third client** of `autoctx-engine` (alongside the
VS Code extension and the Anthropic plugin), and the only one that is
neither an editor nor a hook runtime. Its job is to give humans and
CI scripts the same view the editors get, without needing an editor
host installed:

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
engine validates. See [autoctx-engine.md](./autoctx-engine.md) for
the engine's design.

## CLI surface

```
autoctx --version
autoctx config get [--workspace <path>] [--json]
autoctx config toggle <file> [--workspace <path>]
autoctx config toggle <file> <ruleId> [--workspace <path>]
autoctx instructions list [--workspace <path>] [--json]
autoctx instructions get <name> [--raw] [--workspace <path>]
autoctx instructions toggle <name> [<ruleId>] [--workspace <path>]
autoctx instructions watch [--workspace <path>] [--json]
autoctx workspace detect [<path>] [--json]
autoctx workspace info [--workspace <path>] [--json]
```

What each verb does, on the wire:

- **`config get`** → `Config.Get` over the engine pipe; pretty-print
  by default, `--json` for raw JSON.
- **`config toggle <file> [<ruleId>]`** → `Config.ToggleFile` /
  `Config.ToggleRule`. Writes via the engine, never directly.
- **`instructions list`** → `Instructions.List`. Names + enabled state
  + override flag.
- **`instructions get <name>`** → `Instructions.Get(name)` (projected,
  `[INSTxxxx]` stripped, disabled rules filtered) by default; `--raw`
  uses `Instructions.GetRaw(name)` for the bundled or override bytes
  unmodified.
- **`instructions toggle <name> [<ruleId>]`** → `Config.ToggleFile` /
  `Config.ToggleRule`. Same RPC surface as `config toggle`; this verb
  exists as a discoverability convenience for users thinking in
  "instruction-name" terms.
- **`instructions watch`** → `Instructions.Subscribe`. Streams JSONL
  on stdout: `{event, name, ...}` per change. Exits cleanly on
  Ctrl-C.
- **`workspace detect [<path>]`** → resolves `<path>` (or CWD) to
  a normalised workspace path, spawns or attaches to the engine
  *for that workspace*, then reads `Workspace.Detect` from that
  engine. The engine is workspace-scoped (see
  [autoctx-engine.md → Process scoping](./autoctx-engine.md#process-scoping-one-engine-per-workspace));
  there is no "detect arbitrary path against any engine" mode.
  Useful for repros without needing to inspect the editor's state.
- **`workspace info`** → `Workspace.Info`. Already-detected
  context for the resolved workspace; faster than `detect`
  because the engine has it cached.

What is **deliberately not** in the CLI:

- **No `service` subcommand.** The original design surfaced
  `autoctx service mcps://...` and `autoctx service worker://...` to
  launch processes. With the engine model both vanish: MCP hosts
  launch `autoctx-engine` directly (it is the MCP server), and the
  engine launches workers directly (they are
  `AutoContext.Worker.DotNet[.exe]` etc., already separate binaries).
  The CLI never wears the launcher hat.
- **No `engine` / `daemon` subcommand.** Running the engine is a
  separate binary (`autoctx-engine`), not a mode of `autoctx`.
  Foreground engine debugging is `autoctx-engine --workspace <path>`.
- **No `tools list` / `tasks list`.** Those duplicate engine RPCs
  the editors already drive (`McpTools.*`); add them only if a real
  scripting need surfaces.
- **No in-process projection.** The CLI never re-implements
  `InstructionsFileBodyProjector` or reads `.autocontext.json`
  directly to compute results. If the engine is unreachable, the
  command fails with a clear error and exit code; it does not silently
  fall back to in-process logic.
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

Every CLI subcommand follows the same flow:

1. **Resolve the workspace path.** Either `--workspace <path>` or
   the CWD; normalise (resolve symlinks, lowercase on Windows)
   before hashing.
2. **Compute the engine pipe name.**
   `autocontext-engine-<sha256(normalisedWorkspacePath):0..16>`.
   This is the same hash and prefix the engine binds (see
   [autoctx-engine.md](./autoctx-engine.md#lifecycle)) — clients and
   engine must agree byte-for-byte.
3. **Try to connect.** No pre-flight existence check (Unix-socket
   existence tests are unreliable cross-platform). One try, short
   timeout.
4. **On failure, spawn `autoctx-engine` detached.** Resolved as a
   sibling of the running CLI binary via `AppContext.BaseDirectory`
   (no PATH dependency), launched with `--workspace <normalisedPath>`.
   The CLI uses `Process.Start` with `UseShellExecute = false` and
   redirected (or null) stdio; the spawned engine is not a child in
   any meaningful sense — no parent-child IPC, no inherited handles.
   The engine and the CLI communicate only over the workspace pipe.
5. **Retry connect.** Exponential backoff against two budgets:
   sub-second warm budget, several-second cold budget. Cold-start
   for a self-contained .NET binary is hundreds of milliseconds plus
   an OS hand-off.
6. **`Engine.Hello` handshake.** Single small-budget RPC. Protocol
   version is an integer; mismatch refuses (CLI exits 69). The
   protocol is exact-match and engine + clients ship versioned
   together (see
   [autoctx-engine.md → Lifecycle](./autoctx-engine.md#lifecycle));
   a refusal in production indicates a packaging mismatch and the
   CLI surfaces it rather than negotiating around it.
7. **Issue the actual RPC.** Print result, exit.

The CLI never holds the engine alive; once the RPC completes it
disconnects and the engine drops back into its idle-timer state.

For long-running verbs (`instructions watch`), the CLI also
subscribes to `Engine.Lifecycle` (see
[autoctx-engine.md → Authority model](./autoctx-engine.md#authority-model-engine-owns-clients-cache)):
`reloaded` events trigger a fresh `Instructions.Subscribe`
resubscription against the new generation, and a `shuttingDown`
event is the CLI's cue to exit cleanly with the same exit code
as a normal Ctrl-C (`130`) rather than treating the impending
disconnect as an error.

## Distribution

`autoctx` ships in the same per-RID layout as the engine; both
binaries are siblings. Per-RID layout (re-stated from
[autoctx-engine.md#distribution](./autoctx-engine.md#distribution)
so this doc is self-contained):

```
cli/<rid>/autoctx[.exe]                          # this binary
cli/<rid>/autoctx-engine[.exe]                   # engine binary
cli/<rid>/<framework dlls / runtime files>       # self-contained .NET runtime
cli/<rid>/instructions/<name>.instructions.md    # curated corpus (consumed by the engine, not the CLI)
```

Supported RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-x64`, `osx-arm64`. Bundle locations (the same per-RID tree
shows up in both):

- `<vsix>/cli/<rid>/...` for the VS Code extension.
- `<plugin-root>/cli/<rid>/...` for the Anthropic plugin.
- A standalone GitHub release publishes the same per-RID artefact
  for users who want `autoctx` on their PATH.

The CLI itself does not consume the bundled `instructions/`
directory at runtime — the engine does. The corpus is shipped
alongside `autoctx` only because they share a packaging container.

## Sharing principle (overarching)

The CLI is one of three engine clients; sharing happens at the
**wire-protocol** level, not at the source-code level.

- The CLI is .NET. Its handlers are `System.CommandLine` verbs that
  construct an `AutoctxClient`-equivalent in-process .NET RPC client
  (call it `EngineRpcClient`) and dispatch a single RPC per verb.
- The TS-side `AutoctxClient` (used by the VS Code extension and by
  Anthropic plugin `.cjs` hook scripts) speaks the same wire
  protocol the CLI's `EngineRpcClient` speaks. The two are
  independent implementations of one wire contract; neither is the
  source of truth, the **engine** is.
- **Shells stay thin.** The CLI contains arg / verb parsing, the
  RPC plumbing, output formatting, and the run / teardown loop —
  and nothing else. Logic that is not host-specific belongs in the
  engine. If a CLI verb starts looking like a re-implementation of
  an engine internal, the verb is wrong and the engine RPC should
  grow instead.
- **No invented cross-host seams.** This is *not* a ban on .NET DI.
  Inside `AutoContext.Cli`, use `Microsoft.Extensions.Hosting`
  (`Host.CreateApplicationBuilder`), `IHostedService` for any
  long-running verb (`instructions watch`), `IOptions<T>` from
  `IConfiguration`, and `ILogger<T>` for stderr logs exactly as the
  rest of the .NET solution does. New interfaces only appear when a
  *second concrete* implementation is being added now — not
  hypothetically later.

## Composition contracts

Only one surface from the CLI's composition layer is part of the
design; everything else is implementation choice.

- **`IHostApplicationBuilder.AddAutoContextCli(Action<CliOptions> configure)`**
  is the CLI library's single public entry point. The
  `autoctx` `Program.Main` calls it; tests call it; nothing else
  does. `CliOptions` exposes workspace path resolution, engine-pipe
  override, spawn-disable (for tests that want connect-or-fail
  without spawning), and engine-binary path override (for tests and
  custom layouts). The name mirrors `AddAutoContextEngine` from the
  engine doc — both extension methods live under the
  `AutoContext` umbrella, regardless of the lowercase `autoctx`
  binary name.

The CLI does not expose its `EngineRpcClient` as a library type for
external consumption. If a caller wants to drive the engine from
.NET code, the answer is `AutoContext.Engine`'s own client surface
(or just calling `AutoctxClient` from TS) — not a public API
surfaced through the CLI binary.

## Pitfalls

- **Workspace path resolution divergence.** The CLI must use the
  *exact* same normalisation (resolve symlinks, lowercase on
  Windows) that the engine uses for its pipe name. A one-character
  drift produces a different hash and the CLI talks to a different
  engine. Validator: a round-trip test that hashes a known path on
  both sides and asserts equality.
- **Spawn-on-cold-start signal handling.** The CLI spawns
  `autoctx-engine` detached. SIGINT to the CLI must not propagate
  to the spawned engine; the engine's lifetime is governed by its
  idle timer and its other clients, not by the CLI invocation that
  happened to start it.
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
  `autoctx-engine` resolves as a sibling of `autoctx` from
  `AppContext.BaseDirectory` on every supported RID.
- **Smoke tests.** Mocha-driven smoke runs invoke `autoctx
  --version`, `autoctx workspace detect`, and `autoctx
  instructions list` against a fixture workspace, asserting cold
  spawn → handshake → result → engine idle-shutdown.

## Companion documents

- [autoctx-engine.md](./autoctx-engine.md) — the engine binary the
  CLI is a client of. Wire protocol, RPC surface, lifecycle,
  distribution layout, projection ownership.
- [plan-agent-plugin-discovery-enhancements.md](./plan-agent-plugin-discovery-enhancements.md)
  — the Anthropic plugin (a sibling client of the engine).
