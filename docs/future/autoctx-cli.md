# Plan: `autoctx` CLI (host-agnostic launcher)

## Motivation

Today the MCP server and workers are only spawned by the VS Code extension.
Debugging them standalone (Rider/VS, MCP Inspector, CI) requires reproducing
the extension's spawn dance. A thin CLI exposes the same processes directly
and opens the door to non-VS-Code hosts later.

## Proposed CLI surface

```
autoctx service mcp://<instanceId>
autoctx service worker://<workerId>-<instanceId>
autoctx watch <path>
```

- `<instanceId>` is auto-generated (per-launch GUID/short id) — used to
  namespace pipes, logs, sockets, and discovery.
- `<workerId>` is the registered worker key (e.g. `dotnet`, `workspace`).
- URI-style argument keeps the CLI uniform and forward-compatible with future
  service kinds (`autoctx service something://...`).
- `autoctx watch <path>` runs detection/watching logic against a folder
  without any editor host — useful for repros and CI.

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
- **VS Code TS files: ~50 total**, classified:
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

For TS-side workloads (`autoctx watch`, any service that needs the TS
detection/manifest stack), the CLI ships a small Node entry point
(under `src/AutoContext.Cli.Web/` or similar — name TBD) that the
`autoctx` binary launches as a child process. The Node entry point's
internal structure mirrors the extension:

- `CliComposer` (class) — `compose(inputs: CliCompositionInputs):
  CliGraph`. Inputs: cwd, instanceId, parsed args, root logger, abort
  signal, exit-code reporter.
- `McpServiceComposer`, `WorkerServiceComposer`, `WatchComposer` —
  one composer class per subcommand, each with `compose()` / `run()`
  / `dispose()`.
- All graph members are constructed from `Framework.Web` — same
  classes the extension wires.

### Phase 4 — Optional follow-ups

- Consider extracting `AutoContext.VsCode/instructions/` resource
  loading helpers into `Framework.Web` if the CLI grows an
  `autoctx instructions` subcommand.
- Alternative shells (JetBrains, Neovim, CI) — only when justified.

## Pitfalls

- **Do NOT** move the .NET MCP server into `AutoContext.Framework.Web` —
  that's the TypeScript framework. Keep .NET in .NET projects.
- **Do NOT** conflate "add CLI" with "extract host abstraction" in the same
  change. They're separable; doing both at once balloons scope.
- The CLI will surface hidden assumptions in the .NET side (registry paths,
  log locations, working directory, env vars). Expect a cleanup pass.
- Decide distribution up front: bundled in `.vsix`? `dotnet tool install -g
  autoctx`? standalone GitHub release? Affects RID targeting and single-file
  publish settings.

## Smallest validation slice

1. `src/AutoContext.Cli/AutoContext.Cli.csproj` → `autoctx.exe`.
2. One subcommand: `autoctx service mcp://<instanceId>` that calls extracted
   `McpServerHost.RunAsync`.
3. Wire into `build.ps1` (Compile/Test/Package).
4. Debug MCP server end-to-end via CLI from Rider. If it feels good, expand
   to workers + `watch`.
