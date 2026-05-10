# Plan: `autoctx-engine` (per-workspace state owner and central process)

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
`autoctx-engine`.

## Topology — three clients, one engine

```
                   .autocontext.json   instructions/   workspace files
                            \              |              /
                             \             |             /
                            +-------------------------------+
                            |        autoctx-engine         |
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
                              |         |         +--- Anthropic plugin
                              |         |              (consumer; sees projected results)
                              |         |
                              |         +--- VS Code extension
                              |              (UI surface; toggles files & rules)
                              |
                              +--- autoctx CLI
                                   (debug & scripting client; see autoctx-cli.md)
```

Three clients, three jobs:

- **VS Code extension** is the **UI surface** for the engine. The user
  toggles instruction files on/off, disables individual rules, exports
  workspace overrides, and watches state in the Instructions /
  MCP Tools tree views — all by issuing RPCs against the engine. The
  extension is a pure RPC consumer: it never writes a projected
  instruction file to disk. Instruction delivery to the chat surface
  is the Anthropic plugin's job, not the extension's.
- **Anthropic plugin** is a **consumer**. SessionStart and other hooks
  ask the engine for projected instructions and emit them as
  `additionalContext`. Disabled state is opaque to the plugin: a
  disabled file is simply not returned, a disabled rule is simply not
  in the body. The plugin never sees `.autocontext.json`, never
  resolves overrides, never strips `[INSTxxxx]` tags.
- **`autoctx` CLI** is the **debug & scripting client**. Same wire
  protocol; standalone invocations for repros, CI, and developer
  troubleshooting without an editor host. See [autoctx-cli.md](./autoctx-cli.md).

VS Code is also a host of the Anthropic plugin (per VS Code's own
plugin support); when both surfaces run inside one window they are
two **independent** clients of the same engine, not nested layers.
The plugin's SessionStart hook talks to the engine directly, not via
the extension.

## What the engine absorbs from today's topology

The engine is the new home for everything that today is split between
`AutoContext.Mcp.Server` and the VS Code extension's pipe-server
classes:

| Today | Lives in | Becomes |
|-------|----------|---------|
| `AutoContext.Mcp.Server` (orchestrator + MCP/stdio + worker dispatch + registry) | Standalone process | **Engine internal**; MCP/stdio is one outward transport on the engine |
| `AutoContextConfigManager` (TS, extension) | Extension process | **Engine internal**: `AutoContextConfigStore` (.NET) |
| `InstructionsFilesManager` + `InstructionsFileContentProjector` | Extension process | **Engine internal**: `InstructionsCorpusService` + `InstructionsFileBodyProjector` |
| `LogServer` (sideband pipe) | Extension process | **Gone** — engine writes its own logs to its workspace log file; clients read via `Logs.Tail` RPC if needed |
| `HealthMonitorServer` (sideband pipe) | Extension process | **Gone** — health derived from connected RPC clients on the engine pipe |
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

## Engine binary

`autoctx-engine` is a separate .NET binary, sibling of `autoctx` in
the per-RID distribution layout. It is **not** a subcommand of
`autoctx`; running the engine and running the CLI are different
processes. A binary is one role.

### Process scoping: one engine per workspace

The engine is **always workspace-scoped**. `autoctx-engine`'s
`--workspace <path>` argument is mandatory; there is no
"daemon-wide" mode that serves multiple workspaces. The reasons are
structural, not incidental:

- **State is workspace-shaped.** `.autocontext.json`, the override
  directory `<workspace>/.github/instructions/`,
  workspace-context detection results, and the `disabledTools` /
  `disabledTasks` state are all per-workspace. A single process
  serving N workspaces would just be N independent state machines
  glued into one address space — no shared cache, no shared
  lifecycle, only shared crash blast radius.
- **Lifecycle is workspace-shaped.** Workspaces open and close
  independently across editor windows, sessions, and CI jobs. A
  per-workspace process with idle-timeout shutdown matches that
  natural lifecycle; a multi-tenant daemon would need an internal
  per-workspace eviction policy duplicating the same idle logic.
- **Pipe naming makes this concrete.** Every pipe name is
  `autocontext-engine-<sha256(normalisedWorkspacePath):0..16>`.
  Different workspaces hash to different pipes; the same
  workspace from different hosts hashes to one. The pipe name
  *is* the workspace identity.

Consequences:

- **`Workspace.Detect`** runs on the engine's own configured
  workspace path — the path passed via `--workspace`. It is not a
  general-purpose "detect any path" RPC. The CLI's
  `autoctx workspace detect [<path>]` resolves `<path>` (or CWD),
  spawns the engine *for that path*, and asks the engine for its
  detection result. Asking one engine to detect a different
  workspace is not on the wire.
- **A user with three workspaces open ends up with up to three
  `autoctx-engine` processes**, one per workspace, each idle-timing
  out independently. This is a feature, not a bug — isolation
  matches the existing per-workspace MCP server model.
- **Workspace identity is the path, not the editor.** Two VS Code
  windows opened on the same folder share one engine; a VS Code
  window and a Claude session on the same folder also share one
  engine; the same folder mounted at two different absolute paths
  is two engines. Symlink and case normalisation in pipe-name
  hashing exists precisely to collapse the unintentional
  multi-engine cases.

### Lifecycle

- **Pipe name** is derived deterministically from the absolute
  workspace path:
  `autocontext-engine-<sha256(normalisedWorkspacePath):0..16>`. Path
  normalisation: resolve symlinks, lowercase on Windows. Platform
  prefix (`\\.\pipe\` on Windows, `${os.tmpdir()}/` on POSIX) is
  applied by the pipe transport, not baked into the name.
- **Cold start (try-connect-with-retry, no pre-flight).** A client
  attempts to connect; on failure it asks a single spawner abstraction
  to spawn `autoctx-engine --workspace <path>` detached and retries
  against two budgets, both independent of `Engine.Hello`:
  - **Warm connect (no spawn):** sub-second.
  - **Cold connect (after spawn):** up to a few seconds with
    exponential backoff. A self-contained .NET process binding a pipe
    routinely takes hundreds of milliseconds on first launch, more
    under load.

  No cross-platform pipe-existence pre-flight: existence tests for
  Unix sockets are unreliable; a single try-connect is the canonical
  probe.
- **Concurrent first-connect.** When two clients race, the spawner
  serialises and ensures at most one engine process actually starts;
  the loser of the race re-enters the connect-retry loop against the
  winner. A second engine process that does manage to start must
  detect the existing pipe on bind and exit cleanly (**idempotent
  bind**).
- **Wire-protocol handshake.** After connect, the client issues
  `Engine.Hello` *before* any other RPC, capped by an independent
  short budget. The protocol version is an integer constant bumped on
  every wire-format change. **Compat rule: exact-match required.**
  Engine and client must agree on the integer; mismatch in either
  direction refuses. Each host ships its own bundled
  `autoctx-engine` (inside the VSIX, inside the Claude plugin
  root), and the release process versions hosts together — a
  handshake mismatch in production is a packaging bug, not a
  scenario the protocol tries to recover from. Clients surface
  the refusal as a hard error (CLI exit 69, hook structured
  failure); we do not try to negotiate down.
- **Warm reuse.** Subsequent clients (a second VS Code window on the
  same workspace, a Claude session running concurrently, a one-shot
  CLI invocation) connect to the existing engine. State is consistent
  across all of them.
- **Idle shutdown.** The engine exits after `--idle-timeout` seconds
  with no connected clients (default 300), with a fixed **2-second
  grace period** after the last disconnect to absorb VS Code reload
  churn (extension-host restart, language-service refresh).
- **Crash recovery.** Stale pipe handles surface through the same
  try-connect-with-retry path: a failed connect is treated as "engine
  absent" and triggers a respawn.
- **MCP/stdio facade.** When launched by an MCP host (VS Code's MCP
  manager, Claude Desktop's MCP config), `autoctx-engine` exposes the
  MCP protocol over stdin/stdout *as well as* serving its workspace
  pipe to other clients. The two transports share state. When the
  MCP host disconnects stdio, the engine treats it as a regular
  client disconnect for idle-timeout purposes; pipe clients keep the
  engine alive on their own.

### RPC surface (initial)

- `Engine.Hello` — handshake, returns
  `{ protocolVersion: <int>, engineVersion: <semver> }`. Issued by
  every client immediately after connect; mismatch on the integer
  refuses the engine.
- **`Config.*`** — `Get`, `Subscribe`, `ToggleFile`, `ToggleRule`.
  The VS Code extension is the primary writer (UI toggles); other
  clients are typically subscribers. The engine is the only authority
  for what is enabled / disabled.
- **`Instructions.*`** — `List`, `Get(name)`, `GetAll`, `GetRaw(name)`,
  `Subscribe`. `Get` and `GetAll` return projected bodies (raw
  filtered by `disabledInstructions`, `[INSTxxxx]` tags stripped,
  override preferred over bundled). **`List` includes every
  bundled and override file with its `enabled` flag and override
  status** — the extension's tree view needs disabled entries to
  render the toggle UI. **`GetAll` and `Get` filter to enabled
  files only**: disabled files are omitted from `GetAll`, and
  `Get` returns `null` for a disabled name so consumption-mode
  consumers (Anthropic `additionalContext`, sub-agent
  materialisation) cannot accidentally surface them. `GetRaw` is
  the export-mode escape hatch and ignores enabled state.
- **`Workspace.*`** — `Detect`, `Info`. Workspace-context detection,
  framework / language flags, override file inventory.
- **`McpTools.*`** — `List`. Surfaces the engine's MCP tool catalogue
  (filtered by the same `disabledTools` / `disabledTasks` state) for
  hosts that want to introspect what the engine would advertise to an
  MCP client.
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
- **Future:** `Diagnostics.Run`, `Logs.Tail`, host-specific
  notification channels.

### Naming

- **`<name>`** in `Instructions.{Get,GetRaw,Subscribe}` is the bundled
  file's stem (filename without `.instructions.md`), case-sensitive
  on POSIX, case-preserving on Windows. Override resolution looks for
  `<workspace>/.github/instructions/<name>.instructions.md` and
  prefers the override over the bundled source byte-for-byte.
- **`<workspaceHash>`** is `sha256(normalisedWorkspacePath):0..16` —
  the same prefix used in the pipe name. Reused unmodified for engine
  log paths and OS-cache subdirs so a single hash identifies every
  workspace artefact.

## Authority model: engine owns, clients cache

The engine is the single owner of every piece of AutoContext state
for a workspace — config, instructions corpus, projection,
workspace-context detection, MCP tool catalogue, worker lifecycle.
Clients (VS Code extension, Anthropic plugin, `autoctx` CLI) are
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
  reach the chat surface through the Anthropic plugin's
  SessionStart hook (see below), not through any VS Code
  `chatInstructions` declaration. The extension is a pure RPC
  consumer — tree views, decorations, hovers, and previews all
  consume projected bodies as strings from `Instructions.Get` /
  `Instructions.GetAll`. No projection cache, no static-path
  mirror, no on-disk artefact under `<extensionPath>`. Commands
  that open an instruction *source* in the editor open the
  bundled file at `<extensionPath>/cli/<rid>/instructions/...`
  or the workspace override at
  `<workspace>/.github/instructions/...` — neither is a
  projected body, so neither requires a cache.
- **Anthropic plugin SessionStart hook:** calls `Instructions.GetAll`
  and returns the bodies inline as `additionalContext`. No file ever
  gets written under `${CLAUDE_PLUGIN_ROOT}`. Sub-agents that need
  file paths materialise them under the OS user-cache dir
  (`%LOCALAPPDATA%\autocontext\.cache\<workspaceHash>\` on
  Windows, `$XDG_CACHE_HOME/autocontext/<workspaceHash>/`
  or `~/.cache/autocontext/<workspaceHash>/` on POSIX).
  The hook owns this cache: SessionStart writes, SessionEnd
  cleans, and the engine never reads or writes those paths.

General rule for any future client cache: write under the OS
user-cache dir (`%LOCALAPPDATA%\autocontext\.cache\<workspaceHash>\`
on Windows, `$XDG_CACHE_HOME/autocontext/<workspaceHash>/` or
`~/.cache/autocontext/<workspaceHash>/` on POSIX), never under
the host's install directory (`<extensionPath>`,
`${CLAUDE_PLUGIN_ROOT}`). Install directories are read-only on
managed installs and get wiped on host upgrade; the OS cache root
is writable, survives host upgrades, and gives every client one
consistent place to find and clean its workspace-scoped artefacts.
The Windows path uses an explicit `.cache\` segment because
`%LOCALAPPDATA%` is general app data and the engine already
writes `logs\<workspaceHash>.log` as a sibling under
`%LOCALAPPDATA%\autocontext\`; POSIX paths omit the inner
`.cache` because `$XDG_CACHE_HOME` / `~/.cache/` is already the
cache root by convention.

## Sharing principle (overarching)

**The engine is .NET; hosts are clients.** All projection, config,
and instruction-corpus logic lives in **one** place — the engine
binary, sourced from `AutoContext.Engine/` — written in C#. Every
host (VS Code extension, Anthropic plugin, `autoctx` CLI, future
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
  extension — the Anthropic plugin handles chat-side instruction
  delivery.
- **`AutoctxClient` is the only shared TS class.** A thin pipe-RPC
  client living in `Framework.Web/src/cli/`. Used by the VS Code
  extension and by Anthropic plugin `.cjs` hook scripts. Speaks the
  same wire protocol the engine serves.
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
- **Shells stay thin.** `AutoContext.Cli` and `AutoContext.VsCode`
  contain almost nothing but: arg / activation parsing, host-specific
  surfaces (vscode UI, CLI argv), the `AutoctxClient` plumbing, and
  the run / teardown loop. Logic that is not host-specific belongs
  in the engine.

## Composition contracts

Only two surfaces from the composition layer are part of the design;
everything else is implementation choice that the implementation plan
owns.

- **`IHostApplicationBuilder.AddAutoContextEngine(Action<EngineOptions> configure)`**
  is the engine library's single public entry point. The
  `autoctx-engine` `Program.Main` calls it; tests call it; nothing
  else does. `EngineOptions` exposes workspace path, corpus root
  override, pipe-name override, idle timeout, and (for the MCP/stdio
  facade) whether to enable stdio dispatch — the knobs hosts
  legitimately tune.
- **`AutoctxClient` (TS, `Framework.Web/src/cli/`)** is the only
  shared TS class. Plain class, no DI container, constructed with
  `new` and a workspace path. Speaks the same wire protocol the
  .NET engine serves; that wire protocol is the cross-host seam,
  *not* a class hierarchy.

The extension and the plugin do not share a composer; they share
the engine **binary** (one process per workspace) and the **wire
protocol** (consumed by `AutoctxClient` on the TS side).

## Distribution

The engine must be discoverable from a cold Anthropic plugin
SessionStart hook (no VS Code extension running, no PATH guarantee).
Decision:

- `autoctx-engine` is published per-RID by `dotnet publish -r <rid>
  --self-contained` from `build.ps1 Package`. No Node runtime is
  bundled; the engine and every subcommand are pure .NET.
- **Supported RIDs:** `win-x64`, `win-arm64`, `linux-x64`,
  `linux-arm64`, `osx-x64`, `osx-arm64`. Resolved at runtime from
  `process.platform` + `process.arch` on the TS side and from the
  bundled binary path on the .NET side. Unsupported combinations
  surface a hard error from the spawner; there is no in-process
  fallback path.
- Per-RID artefact layout (the **same** layout in both targets):

  ```
  cli/<rid>/autoctx[.exe]                          # CLI binary
  cli/<rid>/autoctx-engine[.exe]                   # engine binary (this doc)
  cli/<rid>/<framework dlls / runtime files>       # self-contained .NET runtime
  cli/<rid>/instructions/<name>.instructions.md    # curated corpus
  ```

  Both binaries live in the same per-RID directory so `autoctx`
  resolves `autoctx-engine` as a sibling via
  `AppContext.BaseDirectory`. The corpus is a sibling of the
  binaries inside the per-RID directory so the engine resolves it
  from `AppContext.BaseDirectory + "instructions"` without any
  host-supplied path. The corpus is RID-independent in content but
  is duplicated per RID at packaging time — markdown is small and
  the simpler resolver wins.
- Bundle locations:
  - `<vsix>/cli/<rid>/...` for the VS Code extension.
  - `<plugin-root>/cli/<rid>/...` for the Anthropic plugin.
- Hosts resolve the engine binary by joining the resolved root
  (`extensionPath` for VS Code, `${CLAUDE_PLUGIN_ROOT}` for the
  plugin) with `cli/<currentRid>/autoctx-engine[.exe]`. No PATH
  dependency.
- Editable corpus source location: `src/AutoContext.Engine/instructions/`
  (sibling of the engine project so it sits next to the project that
  consumes it). The build copies it into the per-RID staging dir
  during packaging.
- A standalone GitHub release publishes the same per-RID artefact
  for users who want to run `autoctx-engine` directly.

## Pitfalls

- **Engine termination signal.** `autoctx-engine` is launched
  detached, with no inherited stdio handles — every spawner
  (the VS Code extension and Anthropic plugin via Node
  `child_process.spawn(..., { stdio: 'ignore', detached: true })`,
  the `autoctx` CLI via .NET `Process.Start` with
  `UseShellExecute = false` and redirected/null stdio) deliberately
  cuts the engine off from a controlling console so it can outlive
  the spawner. Consequence: `Console.CancelKeyPress` does not
  fire inside the engine. Production termination is
  `--idle-timeout` plus the OS-level signal path
  (`AppDomain.ProcessExit` for SIGTERM / Windows stop). Foreground
  invocations (smoke tests, `dotnet run`) reach the SIGINT path
  normally because they keep the console attached.
- **MCP/stdio idle-timeout interaction.** When the engine is
  launched by an MCP host, stdio is one of its clients. The idle
  watchdog must count an active stdio connection toward the
  client-count gate exactly the same as a pipe connection;
  otherwise an MCP-only session would shut the engine down
  mid-conversation.
- **`autoctx-engine --version` is RID-independent.** Driven by
  `AssemblyInformationalVersionAttribute` set from `version.json`;
  do not bake the RID into the version string. The corpus and the
  version are RID-independent in content.
- **Engine-owned on-disk artefacts.** The engine writes only one
  on-disk artefact per workspace: `logs\<workspaceHash>.log` under
  `%LOCALAPPDATA%\autocontext\` (Windows; equivalents on POSIX).
  Truncated on each engine start, size-rotated, survives shutdown
  for postmortem. No engine-owned cache directory exists — every
  engine cache is in-memory and invalidates on internal events.
  Anything under `%LOCALAPPDATA%\autocontext\.cache\<workspaceHash>\`
  (or its POSIX equivalent) is **client-owned**: the writing
  client is responsible for its lifecycle and cleanup, and the
  engine neither reads nor cleans those paths. Clients must never
  cache under their own install directory (`<extensionPath>`,
  `${CLAUDE_PLUGIN_ROOT}`) — those are read-only on managed
  installs and get wiped on host upgrade. Document any new
  client-owned subdirectory in this list with its owning client
  so cleanup responsibility stays unambiguous.
- **Override survival across upgrades.** A workspace-local
  `<workspace>/.github/instructions/<name>.instructions.md` keeps
  winning silently when the bundled source updates in a release.
  The corpus service emits a warning event when override mtime is
  older than bundled mtime; UIs surface it as a non-fatal hint.
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
  (by an MCP host or by `autoctx`'s spawner). Workers are launched
  by the engine. There is no `autoctx service ...` user surface.

## Implementation phase shape

The phase-by-phase implementation plan — ordering, deliverables,
test plans, and decision rationale — lives in the companion plan
(`plan-autoctx-cli-implementation.md` in repo memory). The design
doc records only the *shape* of the rollout below; when the two
disagree, the design doc wins on architectural intent and the plan
wins on sequencing detail.

Shape:

- **Skeleton.** `AutoContext.Engine` project, empty
  `AddAutoContextEngine`, `autoctx-engine --version`, sibling
  `AutoContext.Cli` skeleton.
- **Engine library populated.** Config store, corpus reader,
  projector, corpus service, workspace detection, pipe-listener /
  idle-watchdog hosted services, RPC handlers, MCP-tool catalogue,
  worker dispatch, MCP/stdio facade. `EngineRpcClient` (.NET) /
  `AutoctxClient` (TS) companions.
- **MCP server retirement.** `AutoContext.Mcp.Server`'s
  `Program.Main` shrinks to delegating into `AddAutoContextEngine`,
  then is deleted entirely once nothing references it. The MCP host
  servers manifest is repointed at `autoctx-engine`.
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

- [autoctx-cli.md](./autoctx-cli.md) — the `autoctx` CLI binary
  (this doc's third client).
- [plan-agent-plugin-discovery-enhancements.md](./plan-agent-plugin-discovery-enhancements.md)
  — the consumer of the engine + `autoctx instructions` work from
  the Anthropic plugin side.
