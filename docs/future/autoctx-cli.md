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
autoctx service mcps://<serverId>/<instanceId>
autoctx service worker://<workerId>/<instanceId>
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
- `<serverId>` is the registered MCP server key (currently the single
  `mcp-server` shipped in `AutoContext.Mcp.Server`). The CLI dispatches
  it opaquely — same pattern as `<workerId>` — so a future second
  server kind ships without CLI changes.
- `<workerId>` is the registered worker key (e.g. `dotnet`, `workspace`).
- URI-style argument keeps the CLI uniform and forward-compatible with future
  service kinds (`autoctx service something://...`). The two existing
  schemes (`mcps`, `worker`) follow the same `<kindId>/<instanceId>`
  shape so parsers stay symmetric: split on `://`, then split the body on
  the **first** `/`. `<kindId>` may contain hyphens (e.g. `mcp-server`);
  `<instanceId>` is opaque to the CLI but recommended to be hex /
  alphanumeric for greppability. `/` rather than `-` separates the two
  fields so a hyphenated `<kindId>` is unambiguous and the URI survives
  any RFC 3986 parser unchanged (unlike a fragment-based `#` separator,
  which standard URI parsers strip).
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

## Surface conventions

These conventions apply uniformly to every subcommand and are enforced
by parser/handler tests. Pin them once; don't relitigate per-subcommand.

- **Exit codes.** `0` success; `1` unhandled exception; `2` parser /
  argument error (`System.CommandLine` default); `64` domain error
  (unknown worker ID, schema validation failure, etc.); `69` daemon
  unreachable after retry budget; `130` SIGINT (terminated by Ctrl-C).
  Codified in `AutoContext.Cli/ExitCodes.cs`.
- **Signals.** `Console.CancelKeyPress` + `AppDomain.ProcessExit` feed a
  single `CancellationTokenSource` that drives `Host.RunAsync(token)`.
  Subcommands cooperate by accepting the token through DI. `watch` and
  `instructions watch` cancel only on SIGINT — stdin-close is *not* a
  cancel signal (the subcommand may run unattended with a closed-stdin
  consumer downstream).
- **Streams.** stdout is reserved for command output (JSON, JSONL,
  version string). Logs and progress go to stderr. Pipes carry only
  JSON-RPC frames. Never mix.
- **Colour.** Respect `NO_COLOR` env var and TTY detection; ANSI
  sequences never appear in piped output. JSON is plain UTF-8.
- **Versioning.** `autoctx --version` reads the
  `AssemblyInformationalVersionAttribute` set by
  `Directory.Build.props` / `version.json`. The `AutoContext.Cli`
  csproj does not declare its own `<Version>`.

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
- **Cold start (try-connect-with-retry, no pre-flight).** A client
  attempts to connect; on failure it asks a single spawner
  abstraction to spawn `autoctx daemon --workspace <path>` detached
  and retries against two budgets, both independent of
  `Daemon.Hello`:
  - **Warm connect (no spawn):** sub-second.
  - **Cold connect (after spawn):** up to a few seconds with
    exponential backoff — a self-contained .NET process binding a
    pipe routinely takes hundreds of milliseconds on first launch,
    more under load.

  **No cross-platform pipe-existence pre-flight.** Existence tests
  for Unix sockets are unreliable; a single try-connect is the
  canonical probe.
- **Concurrent first-connect.** When two clients race, the spawner
  is responsible for serialising and ensuring at most one
  `autoctx daemon` process actually starts; the loser of the race
  re-enters the connect-retry loop against the winner's daemon. A
  second daemon process that does manage to start must detect the
  existing pipe on bind and exit cleanly (**idempotent bind**).
- **Wire-protocol handshake.** After connect, the client issues
  `Daemon.Hello` *before* any other RPC, capped by an independent
  short budget. The protocol version is an integer constant bumped
  on every wire-format change. **Compat rule: exact-match required.**
  Daemon and client must agree on the integer; mismatch in either
  direction refuses. (We do not promise daemon-side
  forward-compatibility for older clients — every host bumps in
  lockstep with the daemon at packaging time. Hooks have a
  permanent disk-read fallback for the bump window.)
- **Warm reuse.** Subsequent clients (a second VS Code window on the same
  workspace, a Claude session running concurrently, a one-shot CLI
  invocation) connect to the existing daemon. State is consistent across
  all of them.
- **Idle shutdown.** The daemon exits after `--idle-timeout` seconds
  with no connected clients (default 300), with a fixed **2-second
  grace period** after the last disconnect to absorb VS Code reload
  churn (extension-host restart, language-service refresh).
- **Crash recovery.** Stale pipe handles surface through the same
  try-connect-with-retry path: a failed connect is treated as "daemon
  absent" and triggers a respawn.

### RPC surface (initial)

- `Daemon.Hello` — handshake, returns
  `{ protocolVersion: <int>, daemonVersion: <semver> }`. Issued by
  every client immediately after connect; mismatch on the integer
  refuses the daemon. CLI subcommands surface exit code 69; hooks
  fall back to disk read.
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
  or any source instruction file changes. Cancellation flows via a
  per-subscription cancel frame in the JSON-RPC framing; without
  one, an abandoned subscription leaks daemon-side until the
  underlying pipe write faults.
- Future: `WorkspaceContext.Get`, `Diagnostics.Run`, `McpTools.List`.

### Naming

- **`<name>`** in `Instructions.{Get,GetRaw,Subscribe}` is the
  bundled file's stem (filename without `.instructions.md`),
  case-sensitive on POSIX, case-preserving on Windows. Override
  resolution looks for
  `<workspace>/.github/instructions/<name>.instructions.md` and
  prefers the override over the bundled source byte-for-byte.
- **`<workspaceHash>`** is `sha256(normalisedWorkspacePath):0..16`
  — the same prefix used in the pipe name. Reused unmodified for
  daemon log paths and OS-cache subdirs so a single hash identifies
  every workspace artefact.

### Projection ownership

The daemon is the **only** writer of projected instruction state. **All
projection happens in-memory**, on every read, from the workspace's
`.autocontext.json` plus the raw corpus — there is no on-disk
projection artefact at all. `Instructions.Get` returns the projected
body as a string over the pipe. This eliminates:

- The `<extensionPath>/instructions/.generated/` shared folder.
- Per-workspace `.workspaces/` projection output directories and the
  metadata generator that wrote them.
- The cross-window / cross-host lock-file dance.
- The read-only-mount problem on Claude plugin installs.

The only on-disk artefacts under `instructions/` are the source
markdown files (`*.instructions.md`) and any user overrides at
`<workspace>/.github/instructions/`.

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

## Composition contracts

Only two surfaces from the composition layer are part of the design;
everything else is implementation choice that the plan owns.

- **`IHostApplicationBuilder.AddAutoContextDaemon(Action<DaemonOptions> configure)`**
  is the daemon library's single public entry point. Hosts that
  want an in-process daemon call this; hosts that only consume the
  daemon over the wire never see it. `DaemonOptions` exposes
  workspace path, corpus root override, pipe-name override, and
  idle timeout — the four knobs hosts legitimately tune.
- **`AutoctxClient` (TS, `Framework.Web/src/cli/`)** is the only
  shared TS class. Plain class, no DI container, constructed with
  `new` and a workspace path. Speaks the same wire protocol the
  .NET daemon serves; that wire protocol is the cross-host seam,
  *not* a class hierarchy.

The extension and the CLI do not share a composer; they share the
daemon **library** (registered through `AddAutoContextDaemon` on the
.NET side) and the **wire protocol** (consumed by `AutoctxClient` on
the TS side).

## Implementation phases

The phase-by-phase implementation plan — ordering, deliverables,
test plans, and decision rationale — lives in the companion plan
(`plan-autoctx-cli-implementation.md` in repo memory; mirrored to
`docs/future/autoctx-cli-implementation-plan.md` on demand). The
design doc records only the *shape* of the rollout below; when the
two disagree, the design doc wins on architectural intent and the
plan wins on sequencing detail.

Shape:

- **Phase 0 — skeleton.** `AutoContext.Cli` project, empty
  `AddAutoContextDaemon`, `autoctx --version`.
- **Phase 1 — first standalone slice.** `autoctx service mcps://`
  proves the CLI shell pattern by extracting the MCP server's
  host loop.
- **Phase 2 — worker subcommand + `autoctx watch`.** Opaque
  `<workerId>` dispatch via the worker registry; standalone
  workspace scanner.
- **Phase 3 — daemon library.** Populates
  `AutoContext.Framework/Daemon/` with the config store, corpus
  reader, projector, corpus service, pipe-listener / idle-watchdog
  hosted services, RPC handlers, and the `DaemonRpcClient` /
  `AutoctxClient` companions.
- **Phase 4 — `autoctx daemon` and `autoctx instructions`,
  extension migration, Claude hook re-pointing, deletes.** No
  dual-mode period for the extension; the in-extension projection
  / config / corpus classes are deleted in the same release that
  ships the daemon. Hooks keep a permanent disk-read fallback.
- **Phase 5 (optional follow-ups).** Alternative shells, daemon-side
  manifest caching, `dotnet tool install -g autoctx`.

## Distribution

The CLI must be discoverable from a cold Claude SessionStart hook (no
VS Code extension running, no PATH guarantee). Decision:

- `autoctx` is published per-RID by `dotnet publish -r <rid>
  --self-contained` from `build.ps1 Package`. No Node runtime is
  bundled; the daemon and every subcommand are pure .NET.
- **Supported RIDs:** `win-x64`, `win-arm64`, `linux-x64`,
  `linux-arm64`, `osx-x64`, `osx-arm64`. Resolved at runtime from
  `process.platform` + `process.arch` on the TS side and from the
  bundled binary path on the .NET side. Unsupported combinations log
  a warning and force the caller into the disk-read fallback.
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

- **Daemon termination signal.** `autoctx daemon` spawned detached
  with `stdio: 'ignore'` has no controlling console;
  `Console.CancelKeyPress` does not fire. Production termination
  is `--idle-timeout` plus the OS-level signal path
  (`AppDomain.ProcessExit` for SIGTERM / Windows stop). The 130
  exit code path is reachable via `autoctx daemon` run in the
  foreground (smoke tests, `dotnet run`) and via `autoctx
  instructions watch` / `autoctx watch` — not via the spawned
  daemon in production.
- **`autoctx --version` is RID-independent.** Driven by
  `AssemblyInformationalVersionAttribute` set from `version.json`;
  do not bake the RID into the version string — the corpus and
  the version are RID-independent in content.
- **Workspace-artefact directory layout** under
  `%LOCALAPPDATA%\autocontext\` (Windows; equivalents on POSIX):
  - `logs\<workspaceHash>.log` — daemon log per workspace.
  - `cache\<workspaceHash>\` — hook materialisation cache for
    sub-agent file paths.
  - `instructions.cache\<workspaceHash>\` lives **inside** the
    VS Code extension root, not under `%LOCALAPPDATA%`, because
    `chatInstructions` resolution is extension-relative.
  Document any new sibling directory in this list before adding it
  to avoid name drift across hosts.
- **Override survival across upgrades.** A workspace-local
  `<workspace>/.github/instructions/<name>.instructions.md` keeps
  winning silently when the bundled source updates in a release.
  The corpus service emits a warning event when override mtime is
  older than bundled mtime; UIs surface it as a non-fatal hint.
- **Do NOT** add `autoctx tools list` or `autoctx tasks list`. MCP tool
  definitions / schemas live in `AutoContext.Mcp.Server/Tools/` +
  `mcp-tools.json`; the worker registry lives in
  `mcp-workers-registry.json`; tasks (`IMcpTask`) live in each
  `AutoContext.Worker.*/Tasks/` folder and `AutoContext.Mcp.Abstractions`.
  The CLI is unaware of any of them: `<workerId>` is opaque, and
  duplicating the MCP server's catalogue inside `autoctx` would fork the
  source of truth.
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

## See also

- [plan-agent-plugin-discovery-enhancements.md](./plan-agent-plugin-discovery-enhancements.md)
  — the consumer of the daemon + `autoctx instructions` work.
