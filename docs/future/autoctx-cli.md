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

**Maximize code reuse between `AutoContext.VsCode`, `autoctx` CLI, and any
future host — without creating abstractions and without leaking VS Code
concepts.**

- **One implementation, one home.** If a class is useful to more than one
  host, it lives in `AutoContext.Framework.Web` and is `new`'d directly by
  each host. No re-export wrappers, no thin pass-through classes.
- **No host-shaped interfaces.** Do not introduce `IHostEnvironment`,
  `IFileSystem`, `IWorkspace`, `IUiHost`, or similar "abstract the editor"
  seams. If a class genuinely needs a capability both hosts can provide,
  pass the concrete dependency through its constructor — don't invent a
  port.
- **No VS Code vocabulary outside `AutoContext.VsCode`.** Names like
  `WorkspaceFolder`, `Disposable` (the `vscode.Disposable` shape),
  `EventEmitter`, `Uri`, `OutputChannel`, command IDs, `when`-clauses, tree
  view contexts, etc. must not appear in `Framework.Web` or in the CLI.
  If a shared class needs a "dispose" or "event" concept, use plain
  Node/standard types (`AsyncDisposable`, `EventTarget`/Node `EventEmitter`,
  `AbortSignal`, `URL`, plain functions) — and only if genuinely needed.
  This rule applies to *concepts* as well as imports: a file with no
  `import vscode` but whose contents are command IDs or tree-view glue
  still belongs in the extension.
- **Duplication is the lesser evil vs. abstraction.** A few lines repeated
  in the VS Code shell and the CLI shell are fine; an interface invented
  to deduplicate them is not. The bar to introduce a shared abstraction
  is: it already exists as a concrete class with one implementation, and
  a *second concrete* implementation is being added now (not hypothetically
  later).
- **Shells stay thin.** `AutoContext.VsCode` and `autoctx` should contain
  almost nothing but: arg/activation parsing, host-specific UI surfaces,
  the composer that wires shared classes from `Framework.Web`, and the
  run/teardown loop. Logic that is not host-specific belongs downstream.

## DI / composition style

The CLI should mirror the **VS Code extension's composition pattern** — not
adopt a container.

> **Style note:** prefer **classes/types** over free functions. New
> composition roots, subcommand wirings, and activation/run sequences
> should be expressed as classes (e.g. `ExtensionComposer`,
> `CliComposer`, `McpServiceComposer`) with explicit methods
> (`compose()`, `run()`, `dispose()`). Same manual `new` wiring inside
> — just packaged as types, not free functions. The VS Code host
> already follows this pattern via `ExtensionComposer`,
> `ExtensionRegistrar`, and `ExtensionActivator`.

The extension uses a manual composition root in
[extension-composition.ts](../../src/AutoContext.VsCode/src/extension-composition.ts):

- A single synchronous, side-effect-free entry point that `new`s every
  long-lived collaborator in a linear pass and returns the wired graph.
- `CompositionInputs` is a small POD (paths, version, `instanceId`,
  workspace root, root logger, event emitters) — it is the host's
  contribution to the graph.
- Activation/registration concerns (awaits, server starts, `vscode.*`
  registrations) live in separate run/registration steps, not inside
  compose.
- Disposables are surfaced via an array the caller is responsible for.
- No reflection, no decorators, no service locator. Tests construct the
  graph with fakes via the same entry point.

### CLI mirror

For `autoctx`:

- A `CliComposer` class with `compose(inputs: CliCompositionInputs):
  CliGraph`.
- `CliCompositionInputs`: cwd, `instanceId`, parsed args/flags, root logger
  sink (stderr/file), cancellation token / abort signal, exit-code reporter.
- One composer **class** per subcommand — `McpServiceComposer`,
  `WorkerServiceComposer`, `WatchComposer` — each owning its own
  `compose()` + `run()` + `dispose()` methods.
- Anything moved into `AutoContext.Framework.Web` is constructed identically
  in both composition roots — same constructors, different inputs. That is
  the *only* sharing mechanism; no abstract host/environment interface.
- Run/teardown phase separated from construction, same as the extension.
- The extension and CLI composers must not import from each other. Their
  only common dependency is `AutoContext.Framework.Web`.
- No DI container (tsyringe/inversify/awilix). Keeping both hosts on the
  same plain-constructor pattern is the whole point.

## Implementation phases

Derived from analyzing `src/AutoContext.Framework.Web/src/` and
`src/AutoContext.VsCode/src/`. Each step is verified with
`.\build.ps1 Compile TS` and `.\build.ps1 Test TS`. No behaviour change
through Phase 1–2.

### Inventory (TS-side, established by analysis)

- **Framework.Web today** — only `logging/` and `pipes/` (LoggerBase,
  ChannelLogger, NullLogger, PipeListener, PipeTransport, codecs).
  Zero vscode dependency.
- **VS Code TS files**, classified:
  - **PURE — moveable as-is.** Utilities (`identifier-factory`,
    `semver`); manifest entries (`*-entry.ts`, `*-runtime-info.ts`,
    `*-item-entry.ts`, `*-category-entry.ts`); manifest containers
    (`instructions-files-manifest.ts`, `mcp-tools-manifest.ts`,
    `servers-manifest.ts`); manifest loaders (`resource-manifest-loader`,
    `instructions-files-manifest-loader`, `mcp-tools-manifest-loader`,
    `servers-manifest-loader`); parsing/metadata
    (`instructions-file-parser`, `instructions-file-metadata-reader`);
    config model (`autocontext-config`, `autocontext-file-manager`).
  - **NEAR-PURE — moveable with one trivial split.**
    `output-channel-logger.ts` — the `vscode.LogOutputChannel` wrapper
    stays; a `ConsoleLogger`/`FileLogger` counterpart implementing
    `ChannelLogger` is added in `Framework.Web`.
  - **VSCODE-CONCEPTUAL — stays in `AutoContext.VsCode`.** Files that
    have no `import vscode` but whose contents only make sense inside
    the extension shell: `ui-constants.ts` (command IDs, view IDs,
    context keys); `tree-view-tooltip.ts`, `tree-view-state-resolver.ts`,
    `tree-view-node-state.ts` (used only by `vscode.TreeDataProvider`
    implementations); `package-instructions-manifest-generator.ts`
    (build-time generator for the extension's `package.json`).
  - **VSCODE-BOUND — split or stay.** `extension*.ts`, all
    `*-tree-provider.ts`, `*-codelens-provider.ts`,
    `*-decoration-manager.ts`, `*-document-provider.ts`; named-pipe
    servers (`log-server`, `health-monitor-server`,
    `worker-control-server`, `autocontext-config-server`,
    `worker-manager`) which use `vscode.Disposable`/`vscode.EventEmitter`
    only as plumbing; `mcp-server-provider` (implements
    `vscode.McpServerDefinitionProvider`); `workspace-context-detector`
    (uses `createFileSystemWatcher` + `findFiles`);
    `autocontext-config-manager` (file watcher + events);
    `autocontext-config-projector` (sets vscode context keys —
    stays); `instructions-files-exporter`,
    `instructions-files-manager`, diagnostics reporter/runner.
    The named-pipe servers, `workspace-context-detector`,
    `autocontext-config-manager`, `mcp-server-provider`, and
    `auto-configurer` are split in Phase 2 — their host-agnostic core
    moves, the vscode-flavoured adapter stays.

### Phase 0 — Repo prep (no code moves)

- Decide `Framework.Web` public-export layout: keep flat
  `index.ts` re-exports; group new exports by namespace folder
  (`config/`, `manifests/`, `detection/`, `services/`).
- Confirm import alias `autocontext-framework-web` resolves cleanly from
  the new CLI consumer (already used by the VS Code extension).
- No `vscode` types may appear in `Framework.Web` `package.json`
  `dependencies`/`devDependencies` — assert via lint/CI grep.

### Phase 1 — Move PURE files

Sub-batches (each compile+test green before the next):

1. **Pure utilities** — `identifier-factory.ts`, `semver.ts`.
2. **Entry types** — `*-item-entry.ts`, `*-category-entry.ts`,
   `*-runtime-info.ts`, `instructions-file-entry.ts`,
   `mcp-tool-entry.ts`, `mcp-task-entry.ts`, `server-entry.ts`.
3. **Manifest containers** — `instructions-files-manifest.ts`,
   `mcp-tools-manifest.ts`, `servers-manifest.ts`.
4. **Manifest loaders** — `resource-manifest-loader.ts` first, then
   `instructions-files-manifest-loader.ts`,
   `mcp-tools-manifest-loader.ts`, `servers-manifest-loader.ts`.
5. **Parsing/metadata** — `instructions-file-parser.ts`,
   `instructions-file-metadata-reader.ts`.
6. **Config (pure parts)** — `autocontext-config.ts`,
   `autocontext-file-manager.ts`.

For each file: move; update `index.ts` re-exports in `Framework.Web`;
rewrite imports in `AutoContext.VsCode/src/` to
`autocontext-framework-web`; delete the old file.

`ui-constants.ts`, `tree-view-tooltip.ts`,
`tree-view-state-resolver.ts`, `tree-view-node-state.ts`, and
`package-instructions-manifest-generator.ts` stay in
`AutoContext.VsCode` per the sharing principle (no vscode-conceptual
content in `Framework.Web`).

### Phase 2 — Split VSCODE-BOUND classes that have a host-agnostic core

Goal: get the named-pipe servers and the config/detection stack into
`Framework.Web` *without* introducing host abstractions or leaking
`vscode.*` types.

1. **Disposable shape.** Replace `vscode.Disposable` usage in shared
   classes with native `Symbol.dispose` / `Symbol.asyncDispose`
   (TS 5.2 explicit resource management). VS Code accepts any object
   with a `dispose()` method, so the extension shell is unaffected.
2. **EventEmitter shape.** Replace `vscode.EventEmitter` in shared
   classes with a tiny in-package `Emitter<T>` (VS Code's own
   implementation is ~20 lines). Lives in `Framework.Web/src/events/`.
3. **Logger split.** Add `ConsoleLogger` / `FileLogger` implementing
   `ChannelLogger` in `Framework.Web`. `OutputChannelLogger` (the
   `vscode.LogOutputChannel` wrapper) stays in `AutoContext.VsCode`.
4. **Move named-pipe servers** — `LogServer`, `HealthMonitorServer`,
   `WorkerControlServer`, `AutoContextConfigServer`, `WorkerManager`,
   plus the spawn/manifest core of `McpServerProvider`. The
   `vscode.McpServerDefinitionProvider` implementation stays as a thin
   extension-side adapter.
5. **Config manager split.** Move the pure parts of
   `AutoContextConfigManager` (load/save, in-memory state, change
   notifications via the new `Emitter`) into `Framework.Web` as
   `AutoContextConfigStore`. The VS Code extension keeps a thin
   `AutoContextConfigManager` that owns the
   `vscode.workspace.createFileSystemWatcher` and forwards changes
   into the store. The CLI gives the store a `node:fs.watch` adapter
   or a manual reload trigger — constructed inside the CLI's composer,
   not behind an interface.
6. **Detection split.** Extract `WorkspaceContextScanner` from
   `WorkspaceContextDetector` — the scanner does globbing + content
   inspection synchronously over a root path (Node `fs`/`fast-glob`,
   no `vscode.workspace.findFiles`). The VS Code extension keeps a
   thin `WorkspaceContextDetector` that wires the scanner to
   `createFileSystemWatcher`. The CLI's `WatchComposer` constructs
   the scanner directly + a Node watcher of its choice.
7. **AutoConfigurer.** Move once its dependencies are in place; it has
   no direct `vscode.*` usage but currently depends on the
   vscode-bound `WorkspaceContextDetector`/`AutoContextConfigManager`
   — it can move only after step 5 and 6.

### Phase 3 — Build the CLI

Project: `src/AutoContext.Cli/AutoContext.Cli.csproj` (.NET) producing
`autoctx.exe`. URI-style command surface as defined above.

**Execution model.** The .NET shell handles arg parsing and routes
subcommands. Subcommands whose work is .NET-native (`service mcp://`,
`service worker://`) run in-process. Subcommands whose work is TS-native
(`watch`, `daemon`, `instructions ...`) launch a bundled Node entry point
(under `src/AutoContext.Cli.Web/` — name TBD) as a child process and
forward stdio / exit code. The Node runtime is bundled alongside the
.NET shell in the per-RID distribution (see Distribution); no system
Node dependency.

The Node entry point's internal structure mirrors the extension:

- `CliComposer` (class) — `compose(inputs: CliCompositionInputs):
  CliGraph`. Inputs: cwd, instanceId, parsed args, root logger, abort
  signal, exit-code reporter.
- `McpServiceComposer`, `WorkerServiceComposer`, `WatchComposer` —
  one composer class per subcommand, each with `compose()` / `run()`
  / `dispose()`.
- All graph members are constructed from `Framework.Web` — same
  classes the extension wires.

### Phase 4 — Daemon + `autoctx instructions`

This is the slice the plugin-discovery plan depends on. Prerequisites:
Phase 1 (pure moves, including `instructions-file-parser`,
`instructions-file-metadata-reader`, `autocontext-file-manager`,
`autocontext-config`) and Phase 2 step 5 (`AutoContextConfigStore`
extracted).

1. **Extract `InstructionsFileBodyProjector`.** Pure-Node, no IO; lifted
   out of `instructions-files-manager.ts`'s projection routine. Inputs:
   raw source string + disabled-id set; output: projected body. Lives in
   `Framework.Web/src/instructions/`.
2. **Extract `InstructionsCorpusReader`.** Pure-Node; given a corpus root
   directory and a workspace root, enumerates curated files, resolves
   override preference (`.github/instructions/<name>` wins over bundled),
   reads raw bodies. Lives in `Framework.Web/src/instructions/`.
3. **`InstructionsCorpusService`.** Composes the reader + projector +
   `AutoContextConfigStore`. Exposes `list()`, `get(name)`, `getAll()`,
   `subscribe(listener)`. Owns the file watchers for the corpus root and
   `.autocontext.json`. Lives in `Framework.Web/src/instructions/`.
4. **`DaemonComposer`.** Wires `AutoContextConfigStore`,
   `InstructionsCorpusService`, and a `PipeListener` exposing the RPC
   surface above. Owns idle-timeout / refcount logic.
5. **`autoctx daemon` subcommand.** Thin CLI shell that constructs
   `DaemonComposer` and runs it.
6. **`autoctx instructions ...` subcommands.** One-shot clients that
   connect to (or spawn) the daemon and emit results to stdout.
7. **VS Code extension migration.** Replace in-process projection with a
   client of the daemon. `InstructionsFilesManager` becomes a cache
   materialiser writing to `<globalStorage>/projected/<hash>/`. CodeLens,
   tree views, and decoration providers read from
   `AutoContextConfigStore` over the pipe (Phase 2 step 5 already gives
   them the store; the pipe transport is what changes).
8. **Claude SessionStart hook.** Reduce to a 20-line shim that calls
   `Instructions.GetAll` and returns the bodies as `additionalContext`.

### Phase 5 — Optional follow-ups

- Alternative shells (JetBrains, Neovim, CI) — only when justified.
- Daemon-side caching of MCP tool manifests / workspace context, if
  cross-host clients show repeated demand.

## Distribution

The CLI must be discoverable from a cold Claude SessionStart hook (no
VS Code extension running, no PATH guarantee). Decision:

- Self-contained `autoctx` binaries are published per-RID by
  `build.ps1 Package` and bundled in two places:
  - `<vsix>/cli/<rid>/autoctx[.exe]` for the VS Code extension.
  - `<plugin-root>/cli/<rid>/autoctx[.exe]` for the Claude plugin.
- Hosts resolve the binary by `path.join(extensionPath | CLAUDE_PLUGIN_ROOT,
  'cli', currentRid(), 'autoctx')`. No PATH dependency.
- A standalone GitHub release publishes the same binaries for users who
  want to run `autoctx` directly.
- `dotnet tool install -g autoctx` is a future option, not required for
  the plugin-discovery work.

## Pitfalls

- **Do NOT** move the .NET MCP server into `AutoContext.Framework.Web` —
  that's the TypeScript framework. Keep .NET in .NET projects.
- **Do NOT** conflate "add CLI" with "extract host abstraction" in the same
  change. They're separable; doing both at once balloons scope.
- The CLI will surface hidden assumptions in the .NET side (registry paths,
  log locations, working directory, env vars). Expect a cleanup pass.
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

## Smallest validation slice

First slice — proves the CLI shell:

1. `src/AutoContext.Cli/AutoContext.Cli.csproj` → `autoctx.exe`.
2. One subcommand: `autoctx service mcp://<instanceId>` that calls extracted
   `McpServerHost.RunAsync`.
3. Wire into `build.ps1` (Compile/Test/Package).
4. Debug MCP server end-to-end via CLI from Rider. If it feels good, expand
   to workers + `watch`.

Second slice — unblocks the plugin-discovery plan:

1. `autoctx daemon --workspace <path>` with the `Instructions.*` RPCs.
2. `autoctx instructions get-all --workspace <path>` as a one-shot client.
3. Claude SessionStart hook calls the one-shot and emits the result as
   `additionalContext`. Round-trip verified end-to-end against a real
   Claude Code session.

## See also

- [plan-agent-plugin-discovery-enhancements.md](./plan-agent-plugin-discovery-enhancements.md)
  — the consumer of the daemon + `autoctx instructions` work.
