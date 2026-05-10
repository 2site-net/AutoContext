# Plan: `autoctx` CLI (host-agnostic launcher and central state owner)

## Motivation

The CLI plays two load-bearing roles:

1. **Standalone launcher** — the MCP server and workers are spawned by the
   VS Code extension today. Debugging them standalone (Rider/VS, MCP
   Inspector, CI) requires reproducing the extension's spawn dance. A thin
   CLI exposes the same processes directly.
2. **Central state owner across hosts** — VS Code is no longer the only
   host. The Claude Code / Claude Desktop plugin (see
   [plan-agent-plugin-discovery-enhancements.md](./plan-agent-plugin-discovery-enhancements.md))
   needs the same view of `.autocontext.json`, the curated instructions
   corpus, projection (file-level and rule-level disable), and override
   resolution that the extension has. Duplicating that logic into a
   SessionStart hook would fork the source of truth. Instead, the CLI
   owns it; both hosts are clients.

The daemon mode (below) is what makes (2) viable: per-workspace pipe
server, low-latency local IPC (~1–3 ms round-trip), single in-memory
state shared by every connected host.

## Proposed CLI surface

```
autoctx service mcp://<instanceId>
autoctx service worker://<workerId>-<instanceId>
autoctx watch <path>
autoctx daemon --workspace <path> [--pipe <name>] [--idle-timeout <seconds>]
autoctx instructions list   --workspace <path>
autoctx instructions get    --workspace <path> <name>
autoctx instructions get-all --workspace <path>
autoctx instructions toggle --workspace <path> <name> [--rule <INSTxxxx>]
autoctx instructions watch  --workspace <path>
```

- `<instanceId>` is auto-generated (per-launch GUID/short id) — used to
  namespace pipes, logs, sockets, and discovery.
- `<workerId>` is the registered worker key (e.g. `dotnet`, `workspace`).
- URI-style argument keeps the CLI uniform and forward-compatible with future
  service kinds (`autoctx service something://...`).
- `autoctx watch <path>` runs detection/watching logic against a folder
  without any editor host — useful for repros and CI.
- `autoctx daemon` is the long-lived per-workspace pipe server. See
  [Daemon mode](#daemon-mode).
- `autoctx instructions ...` is the host-facing surface for the curated
  instructions corpus (list, get projected body for one or all files,
  toggle a file or a single rule, watch for changes). Each subcommand
  auto-discovers the workspace's daemon over the pipe and falls back to
  spawning one on demand if absent. One-shot invocations are valid for
  scripting / CI; interactive hosts should connect to the daemon directly
  via the pipe transport for change-event subscriptions.

## Daemon mode

`autoctx daemon` is a long-lived per-workspace process exposing a named
pipe. It owns the in-memory `AutoContextConfigStore`, the curated
instructions corpus, and the projection logic. Every host (VS Code
extension, Claude SessionStart hook, Claude sub-agent dispatcher, future
JetBrains/Neovim shells) connects as a client and makes RPC calls.

### Lifecycle

- **Pipe name** is derived deterministically from the absolute workspace
  path (`autocontext-daemon-<sha256(normalisedPath):0..16>`) so any host
  that knows the workspace can find or spawn the daemon. Normalisation:
  resolve symlinks, lowercase on Windows. Platform prefix
  (`\\.\pipe\` on Windows, `${os.tmpdir()}/` on POSIX) is applied by the
  pipe transport, not baked into the name.
- **Cold start.** A client connects; if the pipe doesn't exist, the client
  spawns `autoctx daemon --workspace <path>` as a detached child and
  retries the connection with a short backoff (~5 attempts over ~500 ms).
- **Warm reuse.** Subsequent clients (a second VS Code window on the same
  workspace, a Claude session running concurrently, a one-shot CLI
  invocation) connect to the existing daemon. State is consistent across
  all of them.
- **Idle shutdown.** The daemon exits after `--idle-timeout` seconds with
  no connected clients (default 300). Shutdown is cooperative — clients
  send `Disconnect` on graceful close; the daemon counts active sessions.
- **Crash recovery.** Stale pipe handles are detected by a connect-and-ping
  probe; if the named-pipe accept fails (`ECONNREFUSED`/Windows error),
  the client treats the daemon as gone and respawns.

### RPC surface (initial)

- `Config.Get` / `Config.Subscribe` / `Config.Toggle{File,Rule}`.
- `Instructions.List` / `Instructions.Get(name)` / `Instructions.GetAll`
  — returns projected body (raw source filtered by
  `disabledInstructions`, with `[INSTxxxx]` tags stripped, override file
  preferred over bundled when present).
- `Instructions.GetRaw(name)` — returns the unprojected bundled source
  for the requested file. Used by the VS Code extension's
  `InstructionsFilesExporter` when materialising a workspace override
  at `.github/instructions/<name>` (the projection step is
  intentionally skipped because the user is exporting a *baseline* to
  edit, not a runtime view).
- `Instructions.Subscribe` — pushes change events when `.autocontext.json`
  or any source instruction file changes.
- Future: `WorkspaceContext.Get`, `Diagnostics.Run`, `McpTools.List`.

### Projection ownership

The daemon is the **only** writer of projected instruction state. There
is no on-disk projection — `Instructions.Get` returns the projected body
as a string over the pipe. This eliminates:

- The `<extensionPath>/instructions/.generated/` shared folder.
- The cross-window / cross-host lock-file dance.
- The read-only-mount problem on Claude plugin installs.
- Per-workspace projected output directories.

Hosts that need a file path (Claude sub-agent `instructions:` frontmatter,
VS Code `chatInstructions`) get one of two patterns:

- **VS Code:** `chatInstructions` paths in `package.json` are resolved
  relative to the extension root, so the materialisation cache must live
  inside `<extensionPath>/` — not `globalStorage`. The extension calls
  `Instructions.GetAll` on activation and on every `Instructions.Subscribe`
  event, writes the results to `<extensionPath>/instructions.cache/<hash>/`,
  and `chatInstructions` points at the bundled relative path that the
  extension keeps overwriting in place. This is *not* the source of
  truth — it's a host-local materialisation for VS Code's static-path
  API. (Multi-window note: hash-scoped subdirs let concurrent windows on
  different workspaces coexist; same-workspace concurrent windows write
  identical content, so last-writer-wins is harmless.)
- **Claude SessionStart hook:** calls `Instructions.GetAll` and returns
  the bodies inline as `additionalContext`. No file ever gets written
  under `${CLAUDE_PLUGIN_ROOT}`. Sub-agents that need file paths get
  written under the OS cache dir (`%LOCALAPPDATA%\autocontext\<hash>\`
  on Windows, `$XDG_CACHE_HOME/autocontext/<hash>/` or
  `~/.cache/autocontext/<hash>/` on POSIX) per session and cleaned on
  `SessionEnd`. Same materialisation pattern, different cache root.

## Sharing principle (overarching)

**The daemon is .NET; hosts are clients.** All projection, config, and
instruction-corpus logic lives in **one** place —
`AutoContext.Framework/Daemon/` — written in C#. Every host (VS Code
extension, Claude SessionStart hook, Claude sub-agent dispatcher,
future JetBrains/Neovim shells) is a *client* of that daemon. Sharing
happens at the **wire-protocol** level (named-pipe RPC), not at the
source-code level.

Consequences:

- **One implementation, one home.**
  `AutoContextConfigStore`, `InstructionsFileBodyProjector`,
  `InstructionsCorpusReader`, `InstructionsCorpusService`, the
  `DaemonHostedService`, and the `Config.*` / `Instructions.*` RPC
  handlers all live in `AutoContext.Framework/Daemon/`. The `autoctx`
  shell (in `AutoContext.Cli`) registers them with the Generic Host
  container; nothing else does.
- **The VS Code extension keeps no co-projector.** Once Phase 4 lands,
  the extension's TS-side `AutoContextConfigManager`,
  `InstructionsFilesManager`, `InstructionsFileContentProjector`, and
  any in-process projection code are *deleted*. The extension's
  remaining responsibility is wiring `AutoctxClient` (TS) to its tree
  views, codelens providers, decoration providers, and
  `chatInstructions` cache materialiser.
- **`AutoctxClient` is the only shared TS class.** A thin pipe-RPC
  client living in `Framework.Web/src/cli/`. Used by the VS Code
  extension and by Claude `.cjs` hook scripts. Speaks the same wire
  protocol the .NET daemon serves.
- **No invented cross-host seams.** This is *not* a ban on .NET DI —
  it is a ban on inventing portability interfaces (`IFileSystem`,
  `IWorkspace`, a custom `IHostEnvironment`-shaped wrapper) just to
  pretend the C# daemon and the TS extension share code. They don't
  share code; they share a wire protocol. Inside the daemon, use
  `Microsoft.Extensions.Hosting.IHostEnvironment`, `ILogger<T>`,
  `IOptions<T>`, and `IConfiguration` exactly as the rest of the .NET
  solution does. New interfaces only appear when a *second concrete*
  implementation is being added now — not hypothetically later.
- **Duplication is the lesser evil vs. abstraction.** A few lines
  repeated between the C# daemon and a hypothetical second .NET host
  are fine. An interface invented to deduplicate them is not.
- **Shells stay thin.** `AutoContext.Cli` and `AutoContext.VsCode`
  contain almost nothing but: arg/activation parsing, host-specific
  surfaces (vscode UI, CLI argv), the host-builder configuration
  that registers `AutoContext.Framework/Daemon/` classes, and the
  run/teardown loop. Logic that is not host-specific belongs in the
  daemon library.

## Composition style

`AutoContext.Cli` is a standard .NET Generic Host application, in
line with `AutoContext.Mcp.Server`, `AutoContext.Worker.DotNet`, and
`AutoContext.Worker.Workspace`. Each subcommand resolves to a single
`Host.CreateApplicationBuilder(args)` call followed by
subcommand-specific service registrations and `host.RunAsync(ct)`.
`Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`,
`Microsoft.Extensions.Configuration`, and `Microsoft.Extensions.Options`
are used as-is.

- **Daemon (C#, `AutoContext.Framework/Daemon/`).** Exposes an
  extension method
  `IHostApplicationBuilder.AddAutoContextDaemon(DaemonOptions options)`
  that registers `AutoContextConfigStore`, the corpus reader, the
  corpus service, the pipe listener, the idle-timeout watchdog, and
  the RPC handlers as singletons / hosted services. The pipe
  listener and the idle watchdog are `IHostedService`s so the host's
  graceful-shutdown plumbing handles cancellation, drain, and
  disposal automatically. `DaemonOptions` (workspace path, corpus
  root, pipe name, idle timeout) is bound through `IOptions<T>` from
  `IConfiguration` plus command-line overrides.
- **CLI shell (C#, `AutoContext.Cli/`).** One subcommand handler per
  verb — `mcp`, `worker`, `watch`, `daemon`, `instructions` —
  using `System.CommandLine` (already a familiar fit). Each handler
  builds its own host: the `daemon` handler calls
  `AddAutoContextDaemon`; the client subcommands (`instructions ...`)
  register a `DaemonRpcClient` and a short-lived hosted service that
  drives the RPC call and writes results to stdout. The `mcp` /
  `worker` handlers reuse the existing host configuration of
  `AutoContext.Mcp.Server` / `AutoContext.Worker.*` by extracting it
  into shared `AddMcpServer` / `AddWorker` extension methods.
- **`AutoctxClient` (TS, `Framework.Web/src/cli/`).** Plain class,
  no DI container — there is no DI container in the TypeScript
  ecosystem we want to take on. Constructed with `new` and the
  workspace path. Same wire format `DaemonRpcClient` consumes; same
  pipe-name derivation. Used by the VS Code extension and by Claude
  `.cjs` hook scripts.

The extension and the CLI do not share a composer class; they share
the daemon **library** (registered through `AddAutoContextDaemon` on
the CLI side) and the **wire protocol** (consumed by `AutoctxClient`
on the extension side). No TypeScript composition root sits inside
the CLI — the CLI is .NET.

## Implementation phases

Each step is verified with `.\build.ps1 Compile` and
`.\build.ps1 Test`. The .NET shell handles every subcommand
in-process; there is **no Node child process** and no bundled Node
runtime.

### What moves where

- **`AutoContext.Framework/Daemon/`** (new subfolder, .NET) — the
  daemon library: `AutoContextConfigStore`,
  `InstructionsFileBodyProjector`, `InstructionsCorpusReader`,
  `InstructionsCorpusService`, `WorkspaceContextScanner`,
  `DaemonRpcClient`, the pipe-listener and idle-watchdog
  `IHostedService`s, the RPC handlers, and the
  `AddAutoContextDaemon` host-builder extension. (`WorkspaceContextScanner`
  lives here because Phase 2's `autoctx watch` and the daemon's
  future `WorkspaceContext.Get` RPC share the same scan logic.)
- **`AutoContext.Cli/`** (new project, .NET) — the `autoctx` shell.
  Generic-Host application: `Program.cs` + one
  `System.CommandLine` handler per subcommand, each calling
  `Host.CreateApplicationBuilder` and the relevant
  `Add*` extension to wire its services. Depends on
  `AutoContext.Framework`, `AutoContext.Mcp.Server`, and
  `AutoContext.Worker.*` (whichever the selected subcommand needs).
- **`AutoContext.Framework.Web/src/cli/`** (TS) — just
  `AutoctxClient` and its tests. The pipe transport classes already
  in `Framework.Web/src/pipes/` are the wire layer.
- **`AutoContext.VsCode/`** (TS) — keeps everything host-specific.
  After Phase 4 lands, the in-extension projection / config /
  corpus classes (`AutoContextConfigManager`,
  `InstructionsFilesManager`, `InstructionsFileContentProjector`,
  parts of `WorkspaceContextDetector`) are **deleted** and
  replaced by `AutoctxClient` calls. UI-shaped classes (tree
  views, codelens, decorations, `chatInstructions` cache
  materialiser) stay.

Note: today's TS-side projection logic in `instructions-files-manager.ts`
and `instructions-file-content-projector.ts` is *ported* to C#, not
moved. The C# port is the single implementation going forward; the TS
originals are deleted in Phase 4 step 5.

### Phase 0 — Project skeleton

- Create `src/AutoContext.Cli/AutoContext.Cli.csproj` targeting the
  same TFM as the rest of the .NET solution. `Program.cs` builds a
  Generic Host (`Host.CreateApplicationBuilder(args)`) and routes
  to a `System.CommandLine` root command whose `--version` handler
  prints `autoctx <version>`. No subcommand handlers yet.
- Create `src/AutoContext.Framework/Daemon/` folder. Add an empty
  `AddAutoContextDaemon(this IHostApplicationBuilder builder,
  Action<DaemonOptions> configure)` extension method so callers
  have a stable API surface to compile against from Phase 1
  onward, even while the body is a TODO.
- Wire both into `AutoContext.slnx` and `build.ps1` (Compile / Test
  / Package phases).
- Smoke test: `autoctx --version` exits 0.

### Phase 1 — First standalone slice (`autoctx service mcp://`)

Extracts the MCP server's host loop into a reusable form so the
CLI can spawn it without the VS Code extension running.

1. Extract the existing `Program.Main` body of
   `AutoContext.Mcp.Server` into
   `IHostApplicationBuilder.AddMcpServer(McpServerOptions options)`.
   The original `Program.Main` becomes a one-liner that builds a
   host, calls `AddMcpServer`, and returns `host.RunAsync(ct)`.
2. The `mcp` subcommand handler in `AutoContext.Cli/` parses the
   URI-style argument into `McpServerOptions`, builds its own
   host with `AddMcpServer`, and runs it.
3. Smoke test against MCP Inspector (or a stub client) without VS Code
   in the loop.

Value: unblocks Rider/VS debugging of the MCP server end-to-end and
proves the CLI shell pattern before the daemon work begins.

### Phase 2 — Worker subcommand + `autoctx watch`

- The `worker` subcommand handler does for the workers what the
  `mcp` handler did for the MCP server: an `AddWorker` extension
  on `IHostApplicationBuilder` extracted from
  `AutoContext.Worker.*`, called from
  `autoctx service worker://<workerId>-<instanceId>`.
- The `watch` subcommand handler resolves
  `WorkspaceContextScanner` from the host container (registered
  via `AddWorkspaceContextScanner` in
  `AutoContext.Framework/Daemon/`, ported from the extension's
  `WorkspaceContextDetector`), runs it against a path argument,
  and prints the detection result.

No daemon yet — these are one-shot subcommands whose host shuts
down after a single run. Their value is independent debug paths
for the workers and for the workspace scanner.

### Phase 3 — Daemon library (the central piece)

This is the slice the plugin-discovery plan depends on. Builds out
`AutoContext.Framework/Daemon/`. Each step adds services that
`AddAutoContextDaemon` will register:

1. **`AutoContextConfigStore`** (singleton). Loads
   `.autocontext.json`, watches it via `FileSystemWatcher`, exposes
   `Get()` / `Subscribe()`, `ToggleFile(name)` / `ToggleRule(name,
   ruleId)`. Takes `ILogger<AutoContextConfigStore>` and
   `IOptions<DaemonOptions>` through its constructor. Test:
   in-process unit tests with a tmpdir-backed config file.
2. **`InstructionsFileBodyProjector`** (singleton, stateless). Pure
   function-shaped class. Inputs: raw markdown source + disabled-id
   set. Output: projected body with `[INSTxxxx]` tags stripped and
   disabled bullets removed. No IO. Direct port of
   [instructions-file-content-projector.ts](../../src/AutoContext.VsCode/src/instructions-file-content-projector.ts);
   the TS original is deleted at Phase 4 step 5.
3. **`InstructionsCorpusReader`** (singleton). Resolves the corpus
   root from `AppContext.BaseDirectory + "instructions"` (so
   `<cli>/instructions/` is found relative to the published binary
   regardless of the user's working directory — see
   *Distribution*; `IHostEnvironment.ContentRootPath` is *not* used
   here because it defaults to the invocation cwd). Enumerates
   curated files; for each, checks
   `<workspace>/.github/instructions/<name>.instructions.md` and
   prefers the override over the bundled source. Returns raw
   `(name, source)` pairs.
4. **`InstructionsCorpusService`** (singleton). Composes reader +
   projector + `AutoContextConfigStore`. Exposes `List()`,
   `Get(name)`, `GetRaw(name)`, `GetAll()`, `Subscribe(listener)`.
   Owns the `FileSystemWatcher` for the corpus root and for the
   workspace's `.github/instructions/` overrides, plus a
   `Subscribe` on the config store. Re-emits a single coalesced
   change event per 200 ms debounce window.
5. **Pipe RPC handlers.** `Config.*` and `Instructions.*` handlers
   over the existing `AutoContext.Framework/Pipes/` listener.
   JSON-RPC framing. Resolved from DI as transient handlers; the
   listener (an `IHostedService`) dispatches to them per request.
   Per-connection refcount drives the idle-watchdog `IHostedService`.
6. **`AddAutoContextDaemon` extension.** The single public entry
   point. Binds `DaemonOptions`, registers all of the above, and
   adds the pipe-listener and idle-watchdog `IHostedService`s.
   `host.RunAsync(ct)` then handles startup, graceful shutdown,
   and disposal.
7. **`DaemonRpcClient`.** Companion class in
   `AutoContext.Framework/Daemon/` that other .NET hosts (currently
   none planned, but tests need it) use to call the daemon over the
   pipe. Mirrors the `AutoctxClient` (TS) surface so test fixtures
   can hit either transport. Registered through
   `AddAutoContextDaemonClient` for callers that want it from DI.

All seven steps land in `AutoContext.Framework/Daemon/`; nothing
touches `AutoContext.Cli` yet.

### Phase 4 — `autoctx daemon` and `autoctx instructions`

1. **`daemon` subcommand handler** (in `AutoContext.Cli/`). Routes
   `autoctx daemon --workspace <path> [--idle-timeout <s>]` to a
   host built with `AddAutoContextDaemon`, then `host.RunAsync(ct)`.
   Logging goes through the standard
   `Microsoft.Extensions.Logging` providers — console (stderr) by
   default, file provider attached when `--log-file` is supplied.
2. **`instructions` subcommand handler** (in `AutoContext.Cli/`).
   Routes `autoctx instructions list|get|get-all|toggle|watch` to
   a host that registers `AddAutoContextDaemonClient` plus a
   short-lived `IHostedService` driving the call; spawns the
   daemon if absent (cold-start), prints results to stdout.
   `watch` stays connected and streams change events as JSONL
   until the host is cancelled.
3. **`AutoctxClient` (TS).** Build the TS counterpart in
   `Framework.Web/src/cli/`: pipe-name derivation, connect with
   cold-start spawn fallback, `instructions.{list,get,getAll,getRaw}()`,
   `config.{get,toggle*}()`, `subscribe()`. Tests in
   `Framework.Web/tests/cli/` cover both stub-daemon and
   real-daemon round-trips.
4. **VS Code extension migration.** Replace the in-extension
   projection / config / corpus classes with `AutoctxClient` calls.
   `InstructionsFilesManager` becomes a *cache materialiser* that,
   on activation and on every `Instructions.Subscribe` event,
   calls `Instructions.GetAll` and writes the bodies to
   `<extensionPath>/instructions.cache/<workspace-hash>/`. The
   `chatInstructions` paths in `package.json` are repointed at
   that cache. Tree views, codelens, and decoration providers
   pull data from `AutoctxClient` instead of in-process state.
   `InstructionsFilesExporter` calls `Instructions.GetRaw(name)`
   to materialise workspace overrides.
5. **Delete the TS originals.**
   `instructions-files-manager.ts`'s projection writes,
   `instructions-file-content-projector.ts`,
   `autocontext-config-manager.ts`'s state, and the projection
   tests are deleted in the same commit that lands step 4. The
   TS test surface for projection moves to the C# unit tests of
   `InstructionsFileBodyProjector` and `InstructionsCorpusService`.
6. **Claude SessionStart hook.** Becomes a thin `.cjs` shim that
   instantiates `AutoctxClient`, calls `Instructions.GetAll`,
   writes the OS cache, and emits the always-attached pair as
   `additionalContext`.

No dual-mode period: the extension switches to daemon-client in
the same release that ships the daemon.

### Phase 5 — Optional follow-ups

- Alternative shells (JetBrains, Neovim, CI) — only when justified.
- Daemon-side caching of MCP tool manifests / workspace context, if
  cross-host clients show repeated demand.
- `dotnet tool install -g autoctx` packaging.

## Distribution

The CLI must be discoverable from a cold Claude SessionStart hook (no
VS Code extension running, no PATH guarantee). Decision:

- `autoctx` is published per-RID by `dotnet publish -r <rid>
  --self-contained` from `build.ps1 Package`. No Node runtime is
  bundled; the daemon and every subcommand are pure .NET.
- Per-RID artefact layout (the **same** layout in both targets):

  ```
  cli/<rid>/autoctx[.exe]                     # the binary
  cli/<rid>/<framework dlls / runtime files>  # self-contained .NET runtime
  cli/<rid>/instructions/<name>.instructions.md   # curated corpus
  ```

  The corpus is a sibling of the binary inside the per-RID directory
  so the daemon resolves it from `AppContext.BaseDirectory +
  "instructions"` without any host-supplied path. The corpus is
  RID-independent in content but is duplicated per RID at packaging
  time — markdown is small and the simpler resolver wins.
- Bundle locations:
  - `<vsix>/cli/<rid>/...` for the VS Code extension.
  - `<plugin-root>/cli/<rid>/...` for the Claude plugin.
- Hosts resolve the binary by joining the resolved root
  (`extensionPath` for VS Code, `${CLAUDE_PLUGIN_ROOT}` for Claude)
  with `cli/<currentRid>/autoctx[.exe]`. No PATH dependency.
- Editable corpus source location: `src/AutoContext.Cli/instructions/`
  (moved there at Phase 0 so it sits next to the project that
  consumes it). The build copies it into the per-RID staging dir
  during packaging.
- A standalone GitHub release publishes the same per-RID artefact
  for users who want to run `autoctx` directly.
- `dotnet tool install -g autoctx` is a future option (Phase 5),
  not required for the plugin-discovery work.

## Pitfalls

- **Do NOT** port the daemon to TypeScript. The CLI shell is .NET; the
  daemon library lives in `AutoContext.Framework/Daemon/`. The TS side
  ships only `AutoctxClient` and the existing pipe transport.
- **Do NOT** invent cross-host portability seams. Using
  `Microsoft.Extensions.Hosting` (`IHostEnvironment`, `ILogger<T>`,
  `IOptions<T>`, `IConfiguration`) inside the daemon is expected and
  matches the rest of the .NET solution. What we don't do is invent a
  custom `IFileSystem`/`IWorkspace`-style interface that pretends the
  C# daemon and the TS extension share code — they share a wire
  protocol, not a class hierarchy. The TS-side `AutoctxClient`
  stays a plain class, no DI container.
- **Do NOT** conflate "add CLI" with "port projection logic" with
  "migrate the extension". Phases 0–2 (CLI shell + standalone slices),
  Phase 3 (daemon library), and Phase 4 (extension migration) are
  distinct deliverables.
- The CLI will surface hidden assumptions in the .NET side (registry
  paths, log locations, working directory, env vars). Expect a cleanup
  pass during Phase 1.
- **Daemon bootstrap is the chicken-and-egg.** Claude SessionStart runs
  before any extension. The daemon must be self-spawning from a cold
  hook invocation — do not design a flow that requires the VS Code
  extension to start it first.
- **Pipe-name collisions across UNC / case-variant paths.** Normalise the
  workspace path (lowercase on Windows, resolve symlinks) before hashing
  for the pipe name; otherwise two hosts on "the same" workspace get
  different daemons.
- **Concurrent first-connect.** Two hosts racing to spawn the daemon will
  both spawn one. The second daemon must detect the existing pipe on
  startup and exit cleanly (idempotent bind).
- **Corpus drift between RIDs.** The corpus is duplicated per RID in the
  packaged artefact. The build must copy from one source
  (`src/AutoContext.Cli/instructions/`) into every RID staging dir;
  no per-RID corpus edits are permitted. Validator (Phase 3 of the
  plugin plan) asserts byte-equality across RIDs in a build.

## Smallest validation slice

First slice — proves the CLI shell (Phase 1):

1. `src/AutoContext.Cli/AutoContext.Cli.csproj` → `autoctx.exe`.
2. One subcommand: `autoctx service mcp://<instanceId>` that calls
   the extracted `McpServerHost.RunAsync`.
3. Wire into `build.ps1` (Compile/Test/Package).
4. Debug MCP server end-to-end via CLI from Rider. If it feels good,
   expand to workers + `watch` (Phase 2).

Second slice — unblocks the plugin-discovery plan (Phase 3 + Phase 4):

1. `AutoContext.Framework/Daemon/` populated with the corpus service
   and the RPC handlers.
2. `autoctx daemon --workspace <path>` and
   `autoctx instructions get-all --workspace <path>` working
   end-to-end against a real workspace.
3. Claude SessionStart hook calls `AutoctxClient.instructions.getAll()`
   and emits the result as `additionalContext`. Round-trip verified
   against a real Claude Code session.
4. VS Code extension switches to daemon-client mode in the same
   release (Phase 4 step 4); the in-extension projection classes
   are deleted (step 5).

## See also

- [plan-agent-plugin-discovery-enhancements.md](./plan-agent-plugin-discovery-enhancements.md)
  — the consumer of the daemon + `autoctx instructions` work.
