# Implementation Plan: `autocontext-engine`

> **Companion to** [`future/autocontext-engine.md`](./future/autocontext-engine.md).
> That document is the design authority; this document is the rollout
> sequence. When the two disagree, the design wins — open a delta in
> the design first, then update this plan.

## Goals and ground rules

- **Preview release, coordinated cutover.** AutoContext is in preview;
  the whole engine lands in this release. There is no parallel
  transitional release that ships both topologies.
- **One source of truth.** Every phase below cites the design-doc
  section it implements (`design §…`) and the current-codebase files
  it replaces or absorbs (`code …`). Reviewers walk the diff against
  both anchors.
- **Codebase conventions, no drift.** Each phase follows existing
  patterns — `.editorconfig`, `build.ps1` (never bare `dotnet`/`npx`),
  naming and structure mirrored from sibling projects, the
  `Worker.*` shape for new worker projects and the `Framework.Testing`
  shape for shared .NET test harness code, the
  `Nodejs.Core` shape for new TS code.
- **Just-in-time scaffolding.** Introduce a new project, folder,
  file, type, or member only in the phase that *uses* it. No empty
  placeholder projects, no empty class libraries with only a no-op
  extension method, no `Instructions/` / `Resources/` folders
  scaffolded for a later phase to fill. If Phase N+k is the first
  consumer of artefact X, Phase N+k is the one that creates X (and
  its sibling test project). Bare-graph scaffolding ahead of time
  hides intent, leaves dead files in the tree, and produces commits
  that don't earn their own behavioural test. Restructuring code
  that already exists (renames, project splits, file moves) is not
  scaffolding-ahead — it's reorganisation of live code and is
  governed by the *Codebase conventions, no drift* rule above.
- **API hygiene by default.** Across every ctor, method, and
  function signature the engine introduces:
  - **Parameter order is intentional, not incidental.** Arrange
    arguments by the role they play for the call site — required
    before optional, identity / context first, the primary subject
    next, collaborators after, behaviour-shaping flags or
    `CancellationToken` last — and stay consistent with sibling
    APIs on the same type. Drive-by reordering across the
    codebase is not allowed; the order chosen when a member is
    introduced is the contract. `CancellationToken` parameters are
    always named `cancellationToken` in full — never `ct`,
    `cancelToken`, or `token` — so call sites read uniformly
    across every project and signatures stay grep-friendly.
  - **Validate required ctor and method parameters at the boundary.**
    Every required reference parameter is null-checked (and
    range/format-checked where meaningful) before the body
    executes — `ArgumentNullException.ThrowIfNull` (C#) or an
    explicit guard / parsed type (TS). No "defensive deep inside"
    re-validation; one guard at the public surface.
  - **Optional parameters mean genuinely optional.** A parameter
    is `optional` / has a default only when omitting it produces
    well-defined, useful behaviour distinct from passing the
    default explicitly. "Convenience" defaults that paper over
    missing information do not ship; require the caller to be
    explicit and let the type system carry the intent.
  - **Names are readable, not short.** Types, members, locals, and
    parameters are named for the role they play in their context
    — `instructionsCorpusSnapshot` over `corpus`, `pendingReload`
    over `pr`, `workspaceHash` over `wh`. Favour the longer name
    that reads correctly out loud and that a reviewer can reason
    about without opening the call site. Cryptic abbreviations,
    one- or two-letter identifiers, and Hungarian prefixes are
    rejected at review; well-established domain terms
    (`rpc`, `mcp`, `uri`, `json`) stay as-is.
  - **Member order follows StyleCop.** Within every C# type,
    members appear in StyleCop SA1201 order — fields,
    constructors, finalizer, delegates, events, enums, interfaces,
    properties, indexers, methods (including operators and
    conversion operators), then nested types — and within each
    group access goes `public` → `internal` → `protected internal`
    → `protected` → `private protected` → `private` (SA1202),
    with `static` before instance (SA1204). New members slot into
    the correct group rather than being appended; reorder in a
    separate, clearly-scoped commit when an existing file drifts.
- **Logging is a feature, not a courtesy.** Every phase treats
  structured logging as a first-class deliverable of the code it
  introduces, not as a debug-only afterthought. Engine, worker, and
  client code log lifecycle transitions, every RPC handled and
  every envelope kind returned, every snapshot swap and revision
  bump, every subscriber attach / evict, and every error path — at
  appropriate levels, through the unified logging pipeline
  (`design § Engine logging` and the TS / .NET logger sharing
  contract). A phase that adds behaviour without adding the
  corresponding log surface is incomplete.
- **Tests every phase, not "a test phase".** Every phase ships with
  unit tests against the new code. Phases that cross a process
  boundary also ship integration tests (engine spawned via
  `Process.Start` or `child_process.spawn`). No phase merges with red
  tests; no phase defers its tests to a later phase.
- **Each phase compiles and tests green at its boundary** so a
  reviewer can cherry-pick the branch at any phase boundary and the
  full `.\build.ps1 Prepare` is green.
- **Re-read the design before every phase.** Before opening a phase
  branch, re-read the sections of
  [`future/autocontext-engine.md`](./future/autocontext-engine.md)
  that the phase cites (the `design §…` anchors in its header, plus
  any subsections they cross-reference). The design doc evolves
  between phases; an implementation that lines up with last week's
  reading of it is not the same as one that lines up with the
  current text. If the re-read surfaces a contradiction with the
  plan, fix the design first, then update the plan, then implement —
  never silently reconcile in code.
- **Stage → review → fix → commit → next.** Same gate the
  centralized-MCP migration used (see
  `architecture-centralized-mcp.md` § Migration Phases). No phase
  rolls into the next implicitly.
- **No versioning changes.** Version bumps are deliberate and
  user-driven (see `copilot-instructions.md` § Versioning). Phase
  branches do not touch `version.json`, `package.json`, or `.csproj`
  versions.
- **Conventional Commits** for every commit, with the relevant
  `.instructions.md` (`git-commit`, `lang-csharp`, `lang-typescript`,
  `testing`, `dotnet-async-await`, …) applied to the diff.

## Architectural anchors that must hold across every phase

These are properties the design treats as invariants. Every phase
review checks against them; a phase that breaks one rolls back
before merge.

- **P1**: one handler per capability; transports are marshalling
  shims. New surfaces register against the same handler the existing
  surfaces use; no business logic in the shim.
- **P2**: discriminated envelopes for state-bearing reads
  (`ok` / `disabled` / `not-found` / `*-error`), never nullables.
- **P3**: wire shape ≠ engine-internal shape; build-generated
  manifests split into wire (`*.json`) and internal (`*-metadata.json`).
- **P4**: workspace identity is one hash; engine identity adds one
  per-launch UUID (fresh on every spawn; never reused across
  respawns). Pipe names use the flat `<workspaceHash>#<instanceId>`
  segment (OS pipe namespaces are flat); on-disk paths use the
  nested `<workspaceHash>\<instanceId>` segments (POSIX: `/`).
  Never invent a parallel identifier; never flatten the two
  segments back into a workspace-only path.
- **P5**: on-disk path ownership is explicit and exclusive. New
  artefacts land in the table in `design § Distributed bundle layout`
  / `design § P5` with their owner declared.
- **P6**: subscriptions are first-class; clients never poll or watch.
- **P7**: two-layer matching — coarse on the engine, fine on the
  client.
- **P8**: async I/O end-to-end; no sync-over-async on hot paths.
- **P9**: concurrent reads, single-writer per resource,
  snapshot-immutable across reloads; per-subscriber bounded buffer
  with slow-subscriber eviction on every `*.Subscribe`.
- **P10**: in-process async hooks are single-subscriber; cross-process
  fan-out is `*.Subscribe`. No classic .NET `event` slots in framework
  code.

Anything that adds an interface "for portability" needs a second
concrete implementation in the same phase or it doesn't ship. See
`design § Sharing principle`. Test fakes count as a second implementation
when the seam exists specifically to make the production path testable
(e.g. spawn-by-process vs. spawn-in-test); abstractions added for any
other reason still need a real second impl.

## Test strategy (applies to every phase)

- **Unit tests** run against the engine library composed in-process
  via `AddAutoContextEngine(...)` with a per-test workspace path and
  an overridden pipe namespace (library-only `EngineOptions` knob
  documented in `design § Composition contracts`). This is the hot
  path — most phases live here.
- **Integration tests** spawn the published `autocontext-engine`
  binary against a temp workspace. Used for: pipe handshake / cold
  spawn, idle timeout, parent-pid watchdog, MCP-server-only role
  over stdio, cross-process `.autocontext.json` writes, packaging
  smoke. Phase 1 stands the integration harness up; subsequent
  phases extend it.
- **Test-project layout** mirrors the library layout (design §
  *Project layout*) one-to-one:
  `AutoContext.Framework.Pipes.Tests`,
  `AutoContext.Framework.Logging.Tests`,
  `AutoContext.Framework.Protocol.Tests`,
  `AutoContext.Framework.Workers.Tests`,
  `AutoContext.Engine.Core.Tests` (absorbs today's
  `AutoContext.Mcp.Server.Tests` over the course of phases 7 and 16),
  `AutoContext.Client.Core.Tests`, `AutoContext.Engine.Tests`,
  `AutoContext.Build.Tasks.Tests` (round-trip-verifier fixtures and
  task-output assertions; the task is also exercised end-to-end by
  every other project's build).
  Worker test projects are unchanged.
- **TS tests** stay in Vitest, in the same layout
  `AutoContext.Nodejs.Core` and `AutoContext.VsCode` already use.
- **Smoke tests** route through `build.ps1 Test -Smoke` as they do
  today.

## Target structure (end-state after Phase 16)

This is the shape the codebase converges to once every phase has
landed. Use it as a review anchor: each phase below moves the tree
*toward* this picture; nothing in the rollout should produce
intermediate shapes that aren't on a straight line to here. The
source of truth for the architectural rationale is
[`design § Project layout`](./future/autocontext-engine.md#project-layout)
and [`design § Distributed bundle layout`](./future/autocontext-engine.md#distributed-bundle-layout);
this section is the *contract* the implementation plan delivers.

### Scope

This document covers only the projects the `autocontext-engine`
rollout owns end-to-end:

- `AutoContext.Framework.Pipes/` — pipe transport primitives (split
  out of today's `AutoContext.Framework`).
- `AutoContext.Framework.Logging/` — worker-side logger providers:
  `EngineLoggerProvider` (the seam that funnels `ILogger<T>` into
  `Engine.WriteLog`) plus the legacy sideband sink it eventually
  replaces. Folds in the four logging files from today's
  `AutoContext.Worker.Shared`. The canonical wire log envelope
  (`LogRecord`) lives in `Framework.Protocol/` alongside every other
  cross-side DTO.
- `AutoContext.Framework.Protocol/` — cross-side DTOs (the wire
  contract every RPC handler and typed dialer client marshals,
  including the canonical `LogRecord` envelope).
- `AutoContext.Framework.Workers/` — worker-host substrate: the
  `IMcpTask` contract (folded in from `AutoContext.Mcp.Abstractions`),
  `WorkerHostBuilderExtensions`, `WorkerTaskDispatcherService`,
  `WorkerHostOptions`, and `WorkerHealthMonitorService` (hosted service
  that keeps the engine's `health` pipe connection open for the lifetime
  of the worker host).
- `AutoContext.Engine.Core/` — the engine itself as a library
  (every RPC family, the lifecycle hosted service, the stdio MCP-server
  role).
- `AutoContext.Engine/` — the binary host that publishes as
  `autocontext-engine[.exe]` and bundles the corpus + manifests next to
  the binary.
- `AutoContext.Client.Core/` — the dialler as a library (typed RPC
  clients, subscription consumers, find-or-spawn). Listed here because
  it is the in-process counterpart that exercises the engine's wire
  surface from .NET; the shared TS substrate in
  `AutoContext.Nodejs.Core/` is its sibling on the Node side
  (consumed by `AutoContext.VsCode` and `AutoContext.Worker.Web`).

Out of scope for this document (these projects are touched only to
adapt to the new engine, and their per-file shape lives in their own
plans):

- `AutoContext.Worker.*` — workers consume the
  `AutoContext.Framework.Workers` worker-host scaffold; only their
  logger provider changes (it dials the engine's `rpc` pipe via the
  `Engine.WriteLog` RPC). The rest is carry-over.
  (`AutoContext.Mcp.Abstractions` and `AutoContext.Worker.Shared` are
  folded into the four `AutoContext.Framework.*` projects as part of
  this rollout — see Phase 0; `IMcpTask` and the worker-host extensions
  move into `Framework.Workers/`, and the four engine-write-log files
  move into `Framework.Logging/`.)
- `AutoContext.VsCode` and `AutoContext.Nodejs.Core` (shared TS
  substrate) —
  pure consumers of the engine's wire surface.
- `AutoContext.CommandLine` — separate rollout, lives in
  [`autocontext-cli.md`](./future/autocontext-cli.md).

### Source tree

```
src/
  AutoContext.Framework.Pipes/                 # pipe transport primitives
    AutoContext.Framework.Pipes.csproj
    BoundPipeListener.cs
    PipeListener.cs                            # multi-connection server bind
    PipeTransport.cs
    LengthPrefixedFrameCodec.cs
    PipeKeepAliveClient.cs                     # rpc / events keep-alive dialer
    PipeStreamingClient.cs                     # logs / health passive consumer
    PipePersistentExchangeClient.cs
    PipeTransientExchangeClient.cs
    IPipeExchangeClient.cs

  AutoContext.Framework.Logging/               # worker-side logger providers (wire envelope itself lives in Framework.Protocol/)
    AutoContext.Framework.Logging.csproj
    CorrelationScope.cs
    AddEngineLoggerProvider.cs                 # folded in from Worker.Shared/Logging/
    EngineLoggerProvider.cs                    # folded in from Worker.Shared/Logging/
    EngineLogIngestRing.cs                     # folded in from Worker.Shared/Logging/ (bounded ring)
    EngineWriteLogClient.cs                    # folded in from Worker.Shared/Logging/
    # Legacy sideband sink (dragged in Phase 0, deleted in Phase 8 once
    # Engine.WriteLog is the only worker→engine log path):
    PipeLogger.cs
    PipeLoggerProvider.cs
    LoggingClient.cs
    JsonLogGreeting.cs
    LogServerJsonContext.cs

  AutoContext.Framework.Protocol/              # cross-side DTOs + pipe-name shapes (leaf — no references)
    AutoContext.Framework.Protocol.csproj
    PipeName.cs                                # `readonly record struct` implementing IParsable<PipeName> — builder + parser for rpc/events/health/logs × hash#instance
    ServiceAddressFormatter.cs                 # legacy `autocontext.<role>#<instance-id>` formatter — kept until every current-topology dialer flips to PipeName (Phase 12); deleted in Phase 16
    ProtocolVersion.cs                         # Engine.Hello version constant
    LogRecord.cs                               # canonical log-record envelope (timestamp, category, level, …)
    Envelopes/                                 # discriminated-envelope base shapes (P2)
      ResultEnvelope.cs                        # ok | disabled | not-found | *-error union root
      OkEnvelope.cs
      DisabledEnvelope.cs
      NotFoundEnvelope.cs
      ErrorEnvelope.cs
    Messages/                                  # per-RPC request/response DTOs
      EngineMessages.cs                        # Engine.Hello / ListRegistryEntries / Shutdown / WriteLog
      ConfigMessages.cs                        # Config.{Get,Subscribe,ToggleFile,ToggleRule}
      InstructionsMessages.cs                  # Instructions.{List,Get,GetAll,GetAlwaysAttached,GetRaw,SearchContent,Subscribe}
      WorkspaceMessages.cs                     # Workspace.{Detect,Info}
      McpToolsMessages.cs                      # McpTools.{List,Invoke}
      DiscoveryMessages.cs                     # Discovery.{RouteForPrompt,RouteForTool}
      AgentMessages.cs                         # Agent.{SubagentStarted,SubagentStopped,Compacted,ToolUsed,TurnEnded} + Events.Subscribe
      LogsMessages.cs                          # Logs.{GetEngine,TailEngine,GetWorker,TailWorker}
    Serialization/
      ProtocolJsonContext.cs                   # source-generated System.Text.Json context for every DTO above

  AutoContext.Framework.Workers/               # worker-host substrate: task contract + hosted services workers compose into their IHostBuilder
    AutoContext.Framework.Workers.csproj
    IMcpTask.cs                                # folded in from Mcp.Abstractions/
    WorkerHostBuilderExtensions.cs             # folded in from Worker.Shared/Hosting/
    WorkerTaskDispatcherService.cs             # moved from AutoContext.Framework/Workers/
    WorkerHostOptions.cs                       # moved from AutoContext.Framework/Workers/
    WorkerHealthMonitorService.cs              # hosted service that keeps the engine's health pipe connection open for the lifetime of the worker host

  AutoContext.Engine.Core/                # engine as a library
    AutoContext.Engine.Core.csproj
    AddAutoContextEngine.cs                    # IHostApplicationBuilder extension — composition root
    EngineOptions.cs                           # bound from argv (--instance-id, --workspace-root, --idle-timeout, …)
    Infrastructure/                            # horizontal-axis substrate (cross-cutting plumbing); subdivided by kind, not by feature
      Primitives/                              # leaf value types — depended on by everything, depend on nothing themselves
        InstanceId.cs                          # launcher UUID value type — `readonly record struct` implementing IParsable<T>; the `<instanceId>` segment in pipe names and on-disk paths (P4)
    Lifecycle/                                 # this engine's own lifecycle: Hello, Shutdown, watchdogs, own registry entry
      LifecycleService.cs                      # hosted service — owns the four-pipe accept loops
      HelloHandler.cs                          # protocol-version check + greeting payload
      ShutdownHandler.cs                       # graceful drain + Engine.Shutdown RPC
      LifecycleBroadcaster.cs                  # events-pipe state stream (P10)
      # — Engine registry (engine-registry.json mechanics + this engine's own entry) —
      RegistryFile.cs                          # sole writer surface for engine-registry.json (P9 single-writer); owns mutex + FileShare + retry + corrupt-recovery + schema-version contract
      RegistryEntry.cs                         # entry DTO returned/accepted by RegistryFile (engine-internal shape — never on the wire, P3)
      RegistryEntryWriter.cs                   # composes over RegistryFile — appends this engine's entry on start (fresh `instanceId` every spawn; no upsert), removes own entry on graceful shutdown
      # — Watchdogs (process-lifetime guards) —
      IdleTimeoutWatchdog.cs                   # --idle-timeout
      ParentPidWatchdog.cs                     # --parent-pid + Process.StartTime defeat
      InstanceIdCollisionWatchdog.cs           # sanity check — second engine binding under the same --instance-id is a launcher bug (P4 fresh-UUID-per-spawn); routes the diagnostic through CrashWriter, then exits non-zero
      # — Crash handling —
      CrashWriter.cs                           # paranoid last-gasp writer of crash.log — sync File.WriteAllText, no DI, no ILogger, no async, allocation-light; wired into Program.Main top-level try/catch + AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException; never invoked from graceful shutdown paths
    Housekeeping/                              # cache-root upkeep: peer-registration liveness, orphan reaping, retention, foreign-subtree eviction (P5)
      HousekeepingService.cs                   # hosted service — shutdown sweep only, runs after LifecycleService removes own entry + closes pipes; ≤ 1 s deadline budget
      SubtreeRegistryStatus.cs                 # discriminated record hierarchy (Registered | StaleRegistration | Unregistered | Foreign) — P2-shaped contract between scanner, policy, and cleaner
      CacheRootScanner.cs                      # walks the engine cache root, produces SubtreeRegistryStatus per child (pure — no deletion here)
      RegistryEntryReader.cs                   # composes over RegistryFile (Lifecycle/); applies Process.StartTime peer-liveness check, supplies the registration half of CacheRootScanner's classification
      StaleSubtreeCleaner.cs                   # pattern-matches SubtreeRegistryStatus, deletes with concurrent-sweep tolerance (DirectoryNotFoundException counts as success)
      RetentionPolicy.cs                       # single reader of `--retention` — resolves the window per SubtreeRegistryStatus arm (per-entry, unregistered-fallback, foreign)
    Logging/                                   # engine sink, rotation, rotated-file cleanup
      LogSink.cs                               # single-channel ingest, file writer, fan-out
      LogFileWriter.cs                         # writes engine.log / worker-<id>.log
      LogRotator.cs                            # --logging thresholds (normal vs debug)
      RotatedLogCleaner.cs                     # deletes rotated log files past retention inside a live subtree (uses RetentionPolicy from Housekeeping/)
      WorkerLogRouter.cs                       # routes Engine.WriteLog by category prefix
      LogsSubscriptionBroadcaster.cs           # logs pipe + Logs.Tail* fan-out with eviction
      LogsHandlers.cs                          # Logs.GetEngine / TailEngine / GetWorker / TailWorker
    Workspace/                                 # workspace-scoped state — everything keyed by the current workspace root
      Config/                                  # .autocontext.json owner (Config.* wire surface)
        ConfigStore.cs                         # port of TS AutoContextConfigManager
        ConfigSnapshot.cs                      # immutable snapshot type (P9)
        ConfigWatcher.cs                       # FileSystemWatcher + trailing-edge debounce
        ConfigWriter.cs                        # writer mutex + micro-batch coalescer
        DeepEqualityComparer.cs                # self-write suppressor (content hash)
        ConfigHandlers.cs                      # Config.{Get,Subscribe,ToggleFile,ToggleRule}
        ConfigSubscriptionBroadcaster.cs       # snapshot-on-subscribe + per-subscriber bounded buffer
      Context/                                 # ~60-flag detection (Workspace.* wire surface)
        WorkspaceContextDetector.cs            # orchestrator — injected with the four rule-data lists below; runs them, emits result
        WorkspaceHandlers.cs                   # Workspace.{Detect,Info}
        # — Rule data (plain records; each file holds a `static readonly`
        #   table registered in DI as the corresponding `IReadOnlyList<T>`
        #   singleton; no interfaces — substitution is over the data, not
        #   the behaviour) —
        FilePresenceRules.cs                   # IReadOnlyList<FilePresenceRule>     — glob → flag
        NpmContentRules.cs                     # IReadOnlyList<NpmContentRule>       — package.json dep-pattern → flag
        DotNetContentRules.cs                  # IReadOnlyList<DotNetContentRule>    — csproj/PackageReference regex → flag
        FlagActivationEdges.cs                 # IReadOnlyList<FlagActivationEdge>   — [child, parent] transitive activation graph
        # — Derived data (per-Detect outputs; plain records, not DI-registered) —
        FileExtensionsIndex.cs                 # derived ext set, fed to Discovery (P7)
    Instructions/                              # runtime services
      InstructionsCorpusService.cs             # immutable snapshot loader + reloader
      InstructionsFileBodyProjector.cs         # disabled-rule filter, [INSTxxxx] strip, override merge
      InstructionsContentIndex.cs              # in-memory content search index
      InstructionsOverrideWatcher.cs           # .github/instructions/ FS watcher (debounced); produces InstructionsOverrides snapshots
      InstructionsOverrides.cs                 # immutable snapshot of .github/instructions/ inventory (paths + basenames); consumed by InstructionsFileBodyProjector + InstructionsCorpusService
      ApplyToParser.cs                         # comma + brace-expand, extension extraction (shared with the build task via `<Compile Link>`)
      InstructionsHandlers.cs                  # List/Get/GetAll/GetAlwaysAttached/GetRaw/SearchContent/Subscribe
      InstructionsSubscriptionBroadcaster.cs   # snapshot-on-subscribe + disabled-flag re-evaluation
      InstructionsManifestLoader.cs            # reads Resources/instructions-files{,-metadata}.json
    Workers/                                   # worker dispatch (absorbs AutoContext.Mcp.Server/Workers/)
      WorkerManager.cs                         # ensureRunning(workerId) gate
      WorkerProcessSupervisor.cs               # Process.Start + stderr capture under worker.<id>.engine.stderr
      WorkerControlClient.cs                   # dial worker control pipe
      WorkerTaskDispatcher.cs                  # request → worker → response, cancellation forwarding
      WorkersManifestLoader.cs                 # reads Resources/workers.json
      WorkerHealthMonitorServer.cs             # accepts worker keep-alives (engine-side peer of WorkerHealthMonitorService)
    Mcp/                                       # McpTools.List/Invoke handlers + stdio MCP-server role
      McpToolsHandlers.cs                      # shared core (P1) — pipe + stdio both call into this
      McpToolsCatalogService.cs                # filters by disabled state from Config snapshot
      McpToolsRegistryLoader.cs                # reads Resources/mcp-tools-registry.json
      McpToolsRegistrySchemaValidator.cs       # build-time + load-time schema check
      InputSchemaBuilder.cs                    # JSON Schema → ModelContextProtocol types
      McpSdkAdapter.cs                         # MCP-server role: tools/list + tools/call → McpToolsHandlers
      StdioMcpServerEntryPoint.cs              # --mcp-server with-stdio composition root
      PerRequestConfigReader.cs                # MCP-server-role disk re-read of .autocontext.json
    Discovery/                                 # category & extension indices (P7)
      DiscoveryService.cs                      # rebuilt on Instructions.Subscribe + McpTools changes
      CategoryIndex.cs                         # prompt → MCP tool routing
      ExtensionIndex.cs                        # extension → instruction file routing
      DiscoveryHandlers.cs                     # Discovery.{RouteForPrompt,RouteForTool}
    Agent/                                     # Agent.* RPC family
      AgentEventsBroadcaster.cs                # bounded per-subscriber buffers, eviction
      AgentNotificationHandlers.cs             # SubagentStarted/Stopped/Compacted/ToolUsed/TurnEnded
      AgentSessionToolHistogram.cs             # in-memory per-session ToolUsed counts
      AgentEventsHandlers.cs                   # Events.Subscribe pipe-side fan-out
    Rpc/                                       # pipe-side framing shared by every handler folder
      RpcDispatcher.cs                         # method-name → handler delegate table
      RpcRequestReader.cs / RpcResponseWriter.cs
      RpcCancellationBridge.cs                 # client-cancel → CancellationToken plumbing

  AutoContext.Client.Core/                # in-process .NET dialler library (consumed by CLI, .NET tests, future .NET embedders)
    AutoContext.Client.Core.csproj
    AddAutoContextClient.cs                    # IServiceCollection extension
    ClientOptions.cs                           # workspace hash, instance-id, spawn policy
    EngineSpawner/                             # find-or-spawn flow
      IEngineSpawner.cs                        # seam — production = process spawn, tests = in-proc fake
      ProcessEngineSpawner.cs                  # Process.Start against bundled binary
      EngineConnectBudget.cs                   # cold-spawn retry shape
      EngineLocator.cs                         # AppContext.BaseDirectory probe for engine binary
    Rpc/                                       # typed clients (one per surface)
      EngineRpcClient.cs                       # Engine.Hello/Shutdown/ListRegistryEntries/WriteLog
      ConfigRpcClient.cs
      InstructionsRpcClient.cs
      WorkspaceRpcClient.cs
      McpToolsRpcClient.cs
      DiscoveryRpcClient.cs
      AgentRpcClient.cs
      LogsRpcClient.cs
      EngineProtocolException.cs               # raised on Hello version mismatch
    Subscriptions/                             # IAsyncEnumerable<T> consumers (P6, P8)
      EngineLifecycleSubscription.cs
      ConfigSubscription.cs
      InstructionsSubscription.cs
      AgentEventsSubscription.cs
      LogsTailSubscription.cs

  AutoContext.Build.Tasks/                # build-time MSBuild tasks (netstandard2.0 — separate so the runtime libs stay clean)
    AutoContext.Build.Tasks.csproj             # TargetFramework=netstandard2.0; output not shipped with the engine
    InstructionsListBuilder.targets            # imported by AutoContext.Engine.csproj; runs the task during the binary's build
    BuildInstructionsListTask.cs               # MSBuild ITask — scans src/AutoContext.Engine/Instructions/, emits the two manifests into the binary's Resources/
    ApplyToRoundTripVerifier.cs                # build-time invariant: parse(applyTo) then recompose == original (modulo whitespace)
    # ApplyToParser.cs is shared from AutoContext.Engine.Core/Instructions/ApplyToParser.cs
    # via <Compile Include="..\AutoContext.Engine.Core\Instructions\ApplyToParser.cs" Link="ApplyToParser.cs" />
    # so build-time validation and runtime parsing compile the same source. (Not to be confused
    # with dotnet/sourcelink, which is a PDB-to-source debugging feature.)

  AutoContext.Engine/                          # engine binary host
    AutoContext.Engine.csproj                  # publishes as autocontext-engine[.exe]
    Program.cs                                 # entry point — role split (daemon vs. --mcp-server)
    ArgvParser.cs                              # daemon-role + MCP-server-role argument tables, strict rejection
    Role.cs                                    # enum: Daemon | McpServerWithStdio
    DaemonHostFactory.cs                       # composes IHostBuilder → AddAutoContextEngine for the daemon role
    McpServerHostFactory.cs                    # composes the stripped --mcp-server with-stdio host
    StartupBanner.cs                           # ready-marker emission to stderr
    Instructions/                              # bundled corpus — copied next to the binary,
      <curated *.instructions.md files>       # resolved via AppContext.BaseDirectory
                                               # (not embedded resources)
    Resources/                                 # build-generated manifests — copied next to the binary
      instructions-files.json                  #   wire shape (P3)
      instructions-files-metadata.json         #   internal shape (P3 split)
      mcp-tools-registry.json                  #   hand-authored registry
      mcp-tools-registry.schema.json           #   JSON-schema for the registry
      mcp-tools.json                           #   build-time projection of the registry
      workers.json                             #   generated from AutoContext.Worker.* projects

  tests/
    AutoContext.Framework.Pipes.Tests/         # transport primitives — listener, codec, keep-alive, exchange/streaming triad
    AutoContext.Framework.Logging.Tests/       # EngineLoggerProvider, ingest ring, write-log client
    AutoContext.Framework.Protocol.Tests/      # DTO envelope round-trips (including LogRecord), pipe-name builder, source-generated JSON contexts
    AutoContext.Framework.Workers.Tests/       # IMcpTask, WorkerHostBuilderExtensions, WorkerTaskDispatcherService, WorkerHealthMonitorService
    AutoContext.Engine.Core.Tests/             # engine-internal services + every RPC handler + lifecycle + watchdogs
    AutoContext.Client.Core.Tests/             # typed RPC clients, subscription consumers, find-or-spawn flow
    AutoContext.Engine.Tests/                  # binary-host integration: argv parser, role split, ready-marker, end-to-end spawn
    AutoContext.Framework.Testing/             # shared harness reused by engine + worker tests
```

Worker projects, the MCP-abstractions project, the VS Code extension,
and the shared TS substrate (`AutoContext.Nodejs.Core/`) are consumers of the
surfaces defined above; their per-file shape stays in their own
documents and is not enumerated here.

**One type per file.** Each `*.cs` filename above names exactly one
top-level type (class, record, enum, or interface). Where a comment
enumerates RPC methods after a `*Handlers.cs` filename — e.g.
`WorkspaceHandlers.cs # Workspace.{Detect,Info}` — those are the
*public methods* of the single `WorkspaceHandlers` class that
`Rpc/RpcDispatcher.cs` binds into its method-name → delegate table,
not separate types. The bundled-by-feature handler shape (one class
per RPC family rather than one class per RPC method) is the
deliberate trade-off: cohesion over file count, matched to the
delegate-table dispatcher and to the rest of the codebase's
vertical-feature folder axis.

### Runtime bundle layout (shipped artefact)

Per the design's distributed-bundle picture: every shipped host
artefact (VSIX per platform, plugin release per platform,
GitHub-release tarball per RID) embeds the same `engine/` subtree.
The per-RID segment that exists at build-staging time
(`out/engine/<rid>/…`) is **absent** from the shipped product.

```
<host bundle root>/
  engine/
    autocontext-engine[.exe]                   # the binary (one role, two modes)
    <self-contained .NET runtime files>        # dotnet publish -r <rid> --self-contained output
    Instructions/                              # curated corpus side-cars
    Resources/                                 # build-generated manifests (mirror of src tree above)
      instructions-files.json
      instructions-files-metadata.json
      mcp-tools-registry.json
      mcp-tools-registry.schema.json
      mcp-tools.json
      workers.json
    Workers/
      workspace/<entrypoint>                   # one self-contained subdir per worker
      dotnet/<entrypoint>
      web/<entrypoint>
```

Resolution at runtime uses `AppContext.BaseDirectory`; no
host-supplied path threads into the engine for side-car lookup.

### Wire surfaces by end-state owner

| Surface | Owner project | Transport |
|---|---|---|
| `Engine.Hello` / `Shutdown` / `ListRegistryEntries` / `Lifecycle.Subscribe` | `Engine.Core` | `rpc` + `events` |
| `Engine.WriteLog` | `Engine.Core` | `rpc` |
| `Logs.GetEngine` / `TailEngine` / `GetWorker` / `TailWorker` | `Engine.Core` | `rpc` + `logs` |
| `Config.Get` / `Subscribe` / `ToggleFile` / `ToggleRule` | `Engine.Core` | `rpc` + `events` |
| `Workspace.Detect` / `Info` | `Engine.Core` | `rpc` |
| `Instructions.List` / `Get` / `GetAll` / `GetAlwaysAttached` / `GetRaw` / `SearchContent` / `Subscribe` | `Engine.Core` | `rpc` + `events` |
| `McpTools.List` / `Invoke` (pipe RPC) | `Engine.Core` | `rpc` |
| MCP `tools/list` / `tools/call` (stdio) | `Engine.Core` (MCP-server role) | stdio |
| `Discovery.RouteForPrompt` / `RouteForTool` | `Engine.Core` | `rpc` |
| `Agent.*` notifications + `Agent.Events.Subscribe` | `Engine.Core` | `rpc` + `events` |
| Typed .NET clients for every row above | `Client.Core` | dial-side |
| Typed TS clients for every row above (plus TS-side engine-daemon lifecycle) | `Nodejs.Core/src/engine/engine-daemon-manager.ts` | dial-side |

## Phase 0 — Framework restructure

**Goal**: reshape the existing project graph into the four-project
`Framework.*` substrate the rest of the rollout consumes, fold the
two dead-weight projects (`Mcp.Abstractions`, `Worker.Shared`) into
it, and rename the shared TS substrate to its end-state identity.
This phase touches existing code only — every new engine / client /
build-tasks project is created in the phase that first uses it (see
*Just-in-time scaffolding* in the ground rules).

**Design anchors**: `§ Project layout` (Framework substrate row),
`§ Composition contracts`.

**Code touch**:
- Split today's `AutoContext.Framework` project into four sibling
  projects under `src/` (one mechanical pass; touches every
  `<ProjectReference>`, `RootNamespace`, and `using`):
  - `AutoContext.Framework.Pipes/` — receives the existing
    `AutoContext.Framework/Pipes/` files unchanged.
  - `AutoContext.Framework.Logging/` — receives the existing
    `AutoContext.Framework/Logging/` files. References
    `Framework.Pipes` + `Framework.Protocol`.
  - `AutoContext.Framework.Protocol/` — new sub-project (no
    equivalent in today's substrate). Skeletons for the cross-side
    DTOs (protocol-version constant, pipe-name builder, log-record
    envelope, discriminated-envelope base shapes, source-generated
    JSON context). Also receives `AutoContext.Framework/Workers/ServiceAddressFormatter.cs`
    — it's a pure pipe-name string-formatting helper (no I/O, no
    lifetime, no DI), the same wire-shape concern `PipeName.cs`
    owns under the engine topology; parking the legacy formatter next
    to its successor keeps both pipe-name shapes in one place and
    avoids a misleading lineage in Phase 1 where the engine's builder
    would otherwise materialise in a different project than the
    formatter it eventually replaces. Leaf — no other Framework
    references.
  - `AutoContext.Framework.Workers/` — receives
    `AutoContext.Framework/Workers/{WorkerTaskDispatcherService,WorkerHostOptions}.cs`
    and `AutoContext.Framework/Hosting/HealthMonitorClient.cs`
    (renamed in-flight to `WorkerHealthMonitorService.cs` as part of
    the move — the type is an `IHostedService` scoped to the worker
    host's lifetime, not a call-site dialer, so the `*Client` suffix
    mis-cued against the BCL convention where `*Client` reads as
    `HttpClient`-shaped, and the `Worker*` prefix pins what its
    lifetime is actually tied to). References `Framework.Pipes` +
    `Framework.Logging` + `Framework.Protocol`.
  - The empty `AutoContext.Framework` shell project is deleted
    once its files have been redistributed.
- Rename the shared TS substrate project `AutoContext.Framework.Web` →
  `AutoContext.Nodejs.Core` (folder, `package.json` `name`, internal
  imports, every `tsconfig`/`vitest`/`build.ps1` path reference).
  The rename drops the `.Web` suffix — misleading because it
  suggests browser/HTTP — and pairs the shared TS substrate with
  its .NET siblings under a per-runtime `.Core` shape; `Nodejs`
  names the runtime both consumers (`AutoContext.VsCode` extension
  host and `AutoContext.Worker.Web`) actually run on. The unrelated
  `AutoContext.Worker.Web` worker project keeps its name.
- Fold two adjacent projects into the four `AutoContext.Framework.*`
  sub-projects (no behaviour change; pure project-graph
  simplification):
  - `AutoContext.Mcp.Abstractions` (one file: `IMcpTask.cs`) →
    `AutoContext.Framework.Workers/IMcpTask.cs`. Delete the
    `AutoContext.Mcp.Abstractions` project.
  - `AutoContext.Worker.Shared` is split:
    - `Hosting/WorkerHostBuilderExtensions.cs` →
      `AutoContext.Framework.Workers/`.
    - The four logging files (`AddEngineLoggerProvider`,
      `EngineLoggerProvider`, `EngineLogIngestRing`,
      `EngineWriteLogClient`) → `AutoContext.Framework.Logging/`
      (joining the existing wire envelope + legacy
      `PipeLoggerProvider`).
    - Delete the `AutoContext.Worker.Shared` project.
  - Every `Worker.*` project drops its `Mcp.Abstractions` and
    `Worker.Shared` `<ProjectReference>`s and picks up
    `<ProjectReference>`s to all four `AutoContext.Framework.*`
    projects directly.
- New test projects, one per new Framework sub-project:
  `AutoContext.Framework.Pipes.Tests`,
  `AutoContext.Framework.Logging.Tests`,
  `AutoContext.Framework.Protocol.Tests`,
  `AutoContext.Framework.Workers.Tests`.
  Today's `AutoContext.Framework.Tests` is split across the four
  substrate test projects according to which sub-project owns each
  fixture. Test projects for the *new* engine / client / build-tasks
  projects come up alongside those projects in their first-use
  phases.
- `AutoContext.slnx` updated for the four Framework sub-projects,
  the renamed `Nodejs.Core`, and the deletions of `Mcp.Abstractions`
  / `Worker.Shared`. No entries for engine / client / build-tasks
  projects yet — those are added by the phases that introduce them.
- `build.ps1` learns the new Framework project list (compile targets
  only; packaging stays out until Phase 13).

**Tests**:
- Solution builds via `.\build.ps1 Compile`.
- All existing `Worker.*` tests and the split-up Framework substrate
  tests stay green after the rename + consolidation (no behaviour
  change — the diff is purely namespace + project-graph).

**Out of scope**: every new engine / client / build-tasks project
(introduced in their first-use phases); any pipe binding, DI
registration, or executable host.

## Phase 1 — Engine lifecycle substrate

**Goal**: engine binds the four pipes, performs the `Engine.Hello`
handshake, manages its own idle/parent-pid/shutdown lifecycle, and
participates in the shared liveness registry.

**Design anchors**: `§ Lifecycle`, `§ Engine options (CLI surface)`,
`§ RPC surface` (`Engine.Hello`, `Engine.ListRegistryEntries`,
`Engine.Shutdown`, `Engine.Lifecycle.Subscribe`), `§ P4`, `§ P5`,
`§ P8`.

**Code touch**:
- **Create `AutoContext.Engine.Core/`** — new class library, the
  engine as a library. References `Framework.Pipes` +
  `Framework.Logging` + `Framework.Protocol`. Public surface:
  `IHostApplicationBuilder.AddAutoContextEngine(Action<EngineOptions>)`
  composing the hosted services listed below.
- **Create `AutoContext.Engine/`** — new binary project. References
  `AutoContext.Engine.Core` (created in the bullet above). Program
  entry point implements the full argv parser per `§ Engine options`
  (daemon-role table) including `--version`, strict rejection of
  unknown switches with a one-line stderr error.
- **Create sibling test projects** `AutoContext.Engine.Tests` and
  `AutoContext.Engine.Core.Tests` alongside the projects above.
- `AutoContext.slnx` and `build.ps1` learn the two new projects
  (and their test siblings).
- `AutoContext.Framework.Protocol/` — pipe-name builder (workspace
  hash + `<kind>` + `<instanceId>`; normalisation rules in `§ Pipe
  name`), protocol-version integer.
- `AutoContext.Framework.Pipes/` — extended where the four-pipe
  server-side bind needs new transport seams (today's `PipeListener`
  is single-pipe / client-flipped; the engine binds four
  multi-connection servers).
- `AutoContext.Engine.Core/` — hosted services for: pipe accept
  loops (`rpc`, `events`, `health`, `logs` — `logs` is bound here so
  consumers see EOF cleanly, but engine record emission lives in
  Phase 2), `Engine.Hello` handler, `Engine.Lifecycle` broadcaster,
  `Engine.ListRegistryEntries` handler, `Engine.Shutdown` handler,
  `RegistryEntryWriter` (this engine's own entry), idle-timeout
  watchdog, parent-pid watchdog.
- `RegistryFile` — sole owner of `engine-registry.json`,
  applying `§ P9`'s single-writer-per-resource rule on disk. Every
  consumer (this phase's `RegistryEntryWriter`, Phase 2b's
  `RegistryEntryReader`, any future peer-watcher) goes through this
  surface; the writer mutex, `FileShare` choice, exponential-backoff
  retry, atomic-replace strategy, corrupt-file recovery (truncate-
  and-reseed), and schema-version contract live here, not scattered
  across consumers. Born in Phase 1 because Phase 1 is when
  `engine-registry.json` is first written; Phase 2b composes over it
  rather than reaching into the file directly.
- `engine-registry.json` entry lifecycle per
  `§ Housekeeping` and the `engine-registry.json entry lifecycle`
  pitfall: append-on-start (fresh `instanceId` every spawn; no
  upsert), remove-on-graceful-shutdown, leave-stale-on-crash. The
  locking, `FileShare.None` writer window, and exponential-backoff
  reader retry are owned by `RegistryFile` (see above); this
  bullet pins the *lifecycle* of the entry, not the file mechanics.
- The same-`instanceId`-collision rule (`§ Lifecycle` *Concurrent
  first-connect*) — a second engine binding under the same
  `--instance-id` is a launcher bug under the per-launch-UUID
  contract (P4); the engine fails loudly on pipe-bind collision
  with a non-zero exit. `InstanceIdCollisionWatchdog` is the
  fail-fast sanity check enforcing this contract; the design does
  **not** treat the collision as a shape bind has to be idempotent
  against.

**Tests** (unit + integration):
- `Engine.Hello` protocol-version exact-match acceptance and refusal.
- `events` pipe requires the `Hello` envelope before any subscription.
- `health` and `logs` accept connections without the handshake (raw
  read; passive observer rule).
- Four pipes bind atomically; clients can dial each independently.
- Idle timeout fires after the configured window with no `rpc`/`events`
  connections; `--idle-timeout 0` disables the gate.
- `--parent-pid <pid>` watchdog exits cleanly when the parent process
  vanishes; pid-recycling defeat via `Process.StartTime` comparison.
- Registry entry appended on start, removed on graceful shutdown,
  left stale on crash. Two graceful starts of the *same* launcher
  (each minting a fresh `<instanceId>`) leave two distinct entries
  in the registry while both are live; each engine removes its own entry
  on shutdown.
- A crashed engine's entry survives; the next graceful shutdown of
  any peer reaps it after the entry's `retention` window elapses.
- Two engines starting concurrently with the *same* `--instance-id`:
  both fail loudly on pipe-bind collision with non-zero exit — this
  is a launcher-bug fixture, not a normal-operation shape. The
  engine emits a diagnostic log line naming the colliding pipe and
  writes a `crash.log` tombstone under its per-instance subtree
  describing the collision.
- `CrashWriter` produces a parseable `crash.log` under
  `…\<workspaceHash>\<instanceId>\logs\` when an unhandled
  exception escapes `Program.Main`, when a non-main thread raises
  via `AppDomain.UnhandledException`, and when an unobserved
  `Task` faults; graceful `Engine.Shutdown`, idle-timeout, and
  parent-pid watchdog exits produce **no** `crash.log`. A
  deliberately broken write target (read-only directory) does not
  mask the original fault — the process still exits with the
  original non-zero code.
- Corrupt-file recovery: an unparseable
  `engine-registry.json` is truncated and re-seeded by the next start.
- `Engine.Shutdown` returns `{ accepted: true }` immediately, drains
  `rpc`, emits `shutting-down` on `events`, closes pipes, exits 0.
- Integration: spawn the binary, dial each pipe, verify handshake +
  shutdown.

**Out of scope**: cache-root housekeeping sweep — peer-registration
liveness scan, orphan reaping, retention enforcement, foreign-subtree
eviction (Phase 2; needs the nested `<workspaceHash>\<instanceId>`
per-instance subtree shape Phase 2 introduces alongside logging).
Rotating log file production (`engine.log`, `worker-<workerId>.log`;
Phase 2) — note that `crash.log` is in-scope for Phase 1 because
the `CrashWriter` it depends on is wired up here. Worker spawn
(Phase 7).

## Phase 2 — Engine logging pipeline and cache housekeeping

Two equal-tier features land together because they share the
per-instance subtree shape (both write under it) and the
`engine-registry.json` reader (`RegistryEntryReader` consults the
same entries `RegistryEntryWriter` produces). Neither is subordinate to
the other; each gets its own subsection below.

### 2a — Engine logging pipeline

**Goal**: every record the engine emits via `ILogger<T>` lands in
`engine.log` under the per-instance subtree, fans out on the `logs`
pipe and `Logs.TailEngine` RPC subscribers, rotates per `--logging`,
and rotated files are cleaned per `--retention`.

**Design anchors**: `§ Log categories`,
`§ RPC surface` (`Logs.GetEngine`, `Logs.TailEngine`,
`Engine.WriteLog` envelope shape), `§ P9` (slow-subscriber eviction),
`§ Log pipeline backpressure` pitfall.

**Code touch**:
- `AutoContext.Framework.Protocol/LogRecord.cs` — the canonical wire
  envelope (`timestamp`, `category`, `level`, `eventId?`, `message`,
  `properties?`, `exception?`). Phase 2a collapses today's substrate
  pair `LogEntry`/`JsonLogEntry` into this single record under
  Protocol's ownership; `Framework.Logging` keeps the worker-side
  logger provider and the legacy sideband sink, but the envelope
  itself moves to where every other cross-side DTO lives (P1: one
  record envelope; P3: wire shape owned by Protocol).
- `AutoContext.Engine.Core/Logging/` — engine-side log sink:
  one ingest channel, file writer for `engine.log`, fan-out to
  `logs`-pipe and `Logs.Tail*` subscribers (per-subscriber bounded
  buffer; slow subscribers evicted with a terminal
  `{ kind: "evicted", reason: "slow-subscriber" }` frame).
- Rotation per `--logging` thresholds (1k lines / 5 MB normal; 5k /
  25 MB debug); rotated-file naming `engine-<iso8601>.log`.
- `RotatedLogCleaner` deletes rotated files older than the
  `--retention` window during the next rotation. (Per-tenant
  cleanup inside a *live* subtree; whole-subtree cleanup is
  Housekeeping's job — see 2b. The two share `RetentionPolicy`
  as their single reader of the `--retention` option.)
- `Logs.GetEngine` / `Logs.TailEngine` handlers (active file only;
  `opts.lastN`, `opts.since`, `truncated` flag). `crash.log` is
  intentionally **out of scope** for the `Logs.*` RPC surface: it
  is a write-once tombstone produced by Phase 1's `CrashWriter`,
  not a tail-able feed, and is reaped along with the rest of the
  per-instance subtree by 2b housekeeping under `--retention`.

**Tests**:
- Engine `ILogger<T>` records hit `engine.log` and the `logs` pipe
  with the same envelope shape; the wire shape and the on-disk shape
  match byte-for-byte (P1: one record envelope).
- Rotation fires at line-count and at size threshold; both `normal`
  and `debug` thresholds tested.
- `--retention` removes rotated files older than the window during
  the next rotation.
- `Logs.GetEngine` returns active-file content; `truncated: true`
  when the requested range fell off.
- `Logs.TailEngine` server-streams new records; replays from
  `opts.since`.
- Slow-subscriber eviction: a subscriber that doesn't drain gets the
  terminal `evicted` frame and is disconnected; other subscribers and
  the file sink keep progressing.

### 2b — Cache housekeeping

**Goal**: the engine cache root is self-cleaning. Every live engine
classifies the cache root on startup (before pipe bind) and again
on shutdown (after removing its own registry entry), and reaps every
subtree that isn't backed by a live peer entry, subject to the
`--retention` floor. Housekeeping is a first-class engine feature,
not a logging chore.

**Design anchors**: `§ Housekeeping` (the two-clock schedule, the
≤ 1 s deadline budget, the concurrent-sweep contract),
`§ Engine-owned on-disk artefacts` pitfall, `§ P5` (path ownership
is explicit and exclusive).

**Code touch**:
- `AutoContext.Engine.Core/Housekeeping/HousekeepingService` —
  hosted service that runs the **shutdown sweep only**. No startup
  sweep: under the per-launch-UUID contract (P4) every engine's
  `<instanceId>` is fresh on every spawn, so the registry stays
  append-only and there is nothing to reconcile before pipe-bind.
  Cleanup of any peer's leftover subtree happens at this engine's
  own graceful shutdown, after `RegistryEntryWriter` has removed
  this engine's own entry and `LifecycleService` has closed the
  four pipes. Hosted-service registration order pins the
  invariant: register `HousekeepingService` **before**
  `LifecycleService` (and before `RegistryEntryWriter` if it is
  itself registered as a hosted service) so its `StopAsync` runs
  *after* both — reverse-registration order — and the sweep
  observes the on-disk registry in its post-shutdown shape (this
  engine's entry already removed, pipes already closed). Bounded
  by the ≤ 1 s deadline the design specifies; whatever the sweep
  doesn't reach this time, the next graceful shutdown of any
  peer catches.
- `SubtreeRegistryStatus` — discriminated record hierarchy
  (`Registered` | `StaleRegistration` | `Unregistered` | `Foreign`)
  carrying the on-disk subtree path and any registry-entry data
  downstream consumers need. The closed-set classification is
  `§ P2`-shaped (state-bearing read over a finite set of
  possibilities) and is the contract between three consumers:
  `CacheRootScanner` produces it, `RetentionPolicy` resolves the
  window per arm, `StaleSubtreeCleaner` pattern-matches to act. The
  type lets each consumer be tested independently; promoting it from
  an internal switch to a public-shape type also gives diagnostics
  per arm ("reaped N stale-registered, M foreign") for free.
- `CacheRootScanner` — walks the engine cache root and produces a
  `SubtreeRegistryStatus` for each child. Pure: no deletion, no
  policy decisions here. Owns the file-system walk and the registry
  lookup against `RegistryFile` (Lifecycle/, Phase 1). The
  four classification arms:
    1. **Registered** — backed by an `engine-registry.json` entry
       whose pid is alive (`Process.StartTime` defeats pid recycling);
    2. **StaleRegistration** — backed by an entry whose pid is dead
       or recycled;
    3. **Unregistered** — a nested `<workspaceHash>\<instanceId>`
       subtree (matches the canonical shape) but no registry entry
       claims it;
    4. **Foreign** — anything that doesn't match the canonical
       nested per-instance shape: legacy flat
       `<workspaceHash>#<instanceId>` directories from before the
       nested layout, bare `<workspaceHash>` directories from even
       earlier preview builds, or any other shape under the cache
       root. Because the cache root is engine-owned (P5), a foreign
       subtree is by definition stale.
- `RegistryEntryReader` — composes over `RegistryFile`
  (Lifecycle/, Phase 1) to read all entries, applies the
  `Process.StartTime` peer-liveness check, and supplies the
  registration half of `CacheRootScanner`'s classification. The file
  mechanics (locks, retry, corrupt-recovery) live in the
  registry-file type — this reader only adds the liveness check
  on top of the entry data it gets back.
- `StaleSubtreeCleaner` — pattern-matches over
  `SubtreeRegistryStatus`, asks `RetentionPolicy` for the window
  per arm, deletes when outside the window. Concurrent-sweep
  tolerance: a `DirectoryNotFoundException` mid-walk counts as
  success (a peer engine won the race).
- `RetentionPolicy` — the *single* place that reads `--retention`.
  Resolves the window per `SubtreeRegistryStatus` arm: per-entry
  windows honour the entry's own retention if present; unregistered
  and foreign subtrees fall back to this engine's `--retention`.
  Both 2a's `RotatedLogCleaner` and 2b's `StaleSubtreeCleaner` take
  their window from this type (no scattered `EngineOptions.Retention`
  reads).

**Tests**:
- Shutdown sweep classifies a populated cache root correctly across
  all four `SubtreeRegistryStatus` arms against a synthetic
  registry file.
- Stale-registration subtree is deleted once outside the entry's
  retention, preserved while still inside it.
- Unregistered subtree falls back to this engine's `--retention`.
- Foreign subtree (legacy flat `<workspaceHash>#<instanceId>` from
  before the nested layout, or bare `<workspaceHash>` from even
  earlier preview builds) is deleted once it exceeds the retention
  floor, preserved while still inside the retention window.
- Two engines concurrent sweep on the same stale subtree: one wins,
  the other treats `DirectoryNotFoundException` as success; neither
  faults.
- Pid recycling: a registry entry whose pid is now held by an
  unrelated process is correctly classified as stale via
  `Process.StartTime` comparison.
- The ≤ 1 s deadline budget is respected: a deliberately huge cache
  root yields after the budget elapses without blocking shutdown.
- Shutdown sweep runs after `RegistryEntryWriter` has removed this
  engine's own entry and `LifecycleService` has closed the four
  pipes — a peer that starts mid-shutdown does not observe this
  engine's entry as live.
- Integration: spawn two engines against the same cache root,
  hard-kill one (skipping its shutdown sweep), then gracefully
  shut down the survivor; assert the survivor reaps the killed
  engine's subtree as part of its own shutdown sweep, and that no
  live subtree was touched.

**Out of scope** (2a): worker records (Phase 8); `Logs.GetWorker`
/ `Logs.TailWorker` (Phase 8). 2b has no out-of-scope carve-out;
its dependency on Phase 1's `RegistryFile` and
`RegistryEntryWriter` (which together own the on-disk
`engine-registry.json` entries the reader supplies) is declared under
code touch, not deferred work.

## Phase 3 — Config store

**Goal**: engine owns `.autocontext.json`. Reads are concurrent and
lock-free; writes are single-writer with debounce + batch
coalescing; cross-instance writes mediate through `FileShare.None` +
the FS-watcher path; subscribers see one batch envelope per coalesced
write.

**Design anchors**: `§ RPC surface` (`Config.*`),
`§ Reload coalescing: debounce and batch`,
`§ Process scoping` (cross-instance rules), `§ P9`,
`§ Cross-instance .autocontext.json writes race on disk` pitfall.

**Code touch**:
- `AutoContext.Engine.Core/Workspace/Config/ConfigStore` —
  port of today's `AutoContextConfigManager` (TS) into .NET. JSON
  shape unchanged; the dual-casing acceptance (kebab → camel) the
  centralized-MCP plan introduced stays the same.
- `FileSystemWatcher` + per-resource trailing-edge debounce
  (~75–150 ms, `EngineOptions` constant). Reads on timer fire only,
  never inside the watcher callback. Cancellation propagates through
  the engine's root token (P8).
- Deep-equal short-circuit (self-write suppressor): post-debounce
  parse compared by content hash against the current snapshot's
  source hash; equality skips the swap, the fan-out, and the
  revision bump.
- Writer mutex (`SemaphoreSlim`, P9). Writer-side micro-batch window
  (~5–10 ms) folds queued `Config.Toggle*` calls into one
  on-disk write, one snapshot swap, one fan-out envelope of shape
  `{ revision, changes: [...] }`.
- `Config.Get`, `Config.Subscribe`, `Config.ToggleFile`,
  `Config.ToggleRule` handlers.
- Snapshot-on-subscribe (P6) — every new subscriber receives the
  current state as the first frame.

**Tests**:
- Atomic-rename burst on `.autocontext.json` (Windows + WSL shapes)
  coalesces to one reload + one fan-out.
- In-place truncating-save burst also coalesces to one fan-out.
- Local writer-side batch: three back-to-back
  `Config.Toggle*` calls produce one batch envelope with
  `changes.length == 3` in writer-mutex order; revision increments
  once.
- Self-write suppressor: local toggle produces exactly one fan-out
  (writer's), not two (writer + watcher echo).
- Peer-write reload (a second process writes the file): the engine
  reloads once and emits one batch envelope.
- Snapshot immutability: a reader holding a snapshot reference
  observes no mid-flight mutations across a concurrent reload.
- `Config.Subscribe` cold-start frame contains the current state;
  late subscribers don't need a separate `Get`.

**Out of scope**: `Instructions.*` consumers of the disabled state
(Phase 6 — config changes here will fan out to instructions
projection there).

## Phase 4 — Workspace detection

**Goal**: engine runs `Workspace.Detect` on startup against its
own `--workspace` path, exposes the result via `Workspace.Detect` and
`Workspace.Info`, and produces the `extensions[]` index the coarse
`applyTo` filter consumes in Phase 6.

**Design anchors**: `§ RPC surface` (`Workspace.*`),
`§ P7` (coarse/fine match split), the ~60-flag table in
`§ RPC surface` *`Detect` return shape*.

**Code touch**:
- `AutoContext.Engine.Core/Workspace/Context/WorkspaceContextDetector` —
  port of today's `workspace-context-detector.ts`. The four
  declarative tables (`fileRules`, `npmContentRules`,
  `dotnetContentRules`, `flagActivationRules`) land as four
  `static readonly` lists of plain records — `FilePresenceRule`,
  `NpmContentRule`, `DotNetContentRule`, `FlagActivationEdge` —
  registered in DI as the corresponding `IReadOnlyList<T>`
  singletons. No `I*Rules` interfaces, no provider types: the
  detection probes are three different *operations* (filesystem
  glob, package.json dep-set match, csproj regex match) that
  happen to share a list shape, and the activation graph is a
  fourth concept entirely (graph closure, no FS, no file content)
  — collapsing them under one `I*Rules` interface would be
  shape-driven naming, not concept-driven naming. The detector
  takes the four lists via constructor injection and switches on
  rule kind internally (same shape as today's TS port). Per-flag
  test fixtures compose the detector with trimmed lists; the
  substitution surface is *data*, not *behaviour*, which is what
  `IReadOnlyList<T>` already gives us — the "no interface without
  a second impl" invariant therefore never fires here, because no
  interface is introduced. Same flag names, same globs, same
  regex patterns, same `[child, parent]` activation edges as the
  TS port; no rule expansion, the existing ~60-flag set is the
  contract.
- Derived `extensions[]` is a plain record produced by one `Detect`
  call — owned by the result, not DI-registered (no shared lifetime
  to manage). Built from the same glob rules so a new file-rule
  flag automatically extends the extension set.
- `Workspace.Detect` and `Workspace.Info` handlers. The detector
  has **no** business with `.github/instructions/` content —
  that inventory is owned by `Instructions/InstructionsOverrideWatcher`
  (Phase 6) and reachable via `Instructions.List`. The TS reference
  port (`src/AutoContext.VsCode/src/workspace-context-detector.ts`)
  already enforces this split: `workspace-context-detector.ts` does
  not scan overrides; `instructions-files-override-watcher.ts` does.
  The .NET port mirrors that separation of concerns.

**Tests**:
- One fixture-per-flag test asserting each rule fires only on its
  declared trigger.
- Activation cascade: `hasNextJs` triggers `hasReact` triggers
  `hasNodeJs` without re-running the file scans.
- `extensions[]` derivation matches the union of every active
  file-rule flag's extensions; content-rule flags contribute none.
- `Workspace.Info` returns engine-process metadata distinct from
  `Detect`.
- `Workspace.Detect` return shape carries **no** `overrides` field
  (negative-shape test against the wire contract): a workspace with
  files under `.github/instructions/` produces the same `Detect`
  envelope as a workspace without — the detector is blind to
  override content.

**Out of scope**: `Discovery.RouteForPrompt` extension index (Phase 9
— consumes the same data but lives in its own service).

## Phase 5 — Instructions corpus build-time pipeline

**Goal**: a single build-time pass over `src/AutoContext.Engine/Instructions/`
produces both `Resources/instructions-files.json` (wire shape) and
`Resources/instructions-files-metadata.json` (engine-internal
indices). The `applyTo` parser ships here, parses only, and is
round-trip-verified per fixture.

**Design anchors**: `§ Resource manifests`,
`§ applyTo` matching subsection under `Instructions.*`,
`§ P3` (wire ≠ internal), `§ applyTo parser pitfall`.

**Code touch**:
- **Create `AutoContext.Build.Tasks/`** — new `netstandard2.0` class
  library, plus its sibling test project
  `AutoContext.Build.Tasks.Tests`. Added to `AutoContext.slnx` and
  `build.ps1` in the same change. The implementations described
  below land in this project as it is introduced.
- Curated instruction corpus moves to
  `src/AutoContext.Engine/Instructions/` — the binary host owns the
  side-cars (P5). Today the corpus is co-located with the VS Code
  extension at `src/AutoContext.VsCode/instructions/`; the move is
  part of this phase because the engine binary is now the owner and
  the files ship next to the binary (resolved at runtime via
  `AppContext.BaseDirectory`, not embedded resources). The
  `Instructions/` and `Resources/` side-car folders under
  `src/AutoContext.Engine/` are created here too — first phase that
  actually populates them.
- `InstructionsListBuilder` — MSBuild task lives in a dedicated
  build-tasks project (`AutoContext.Build.Tasks/`, netstandard2.0)
  rather than the engine runtime library, because MSBuild ITask
  implementations must load under both MSBuild-Full-Framework and
  MSBuild-Core and because the task DLL + round-trip verifier ship
  nothing at runtime. The `.targets` file is imported by
  `AutoContext.Engine.csproj` (binary host — the project that
  owns the output `Resources/` folder); the task writes
  `instructions-files.json` + `instructions-files-metadata.json`
  into `src/AutoContext.Engine/Resources/`. The `applyTo` parser
  is shared from `AutoContext.Engine.Core/Instructions/` into the
  build-tasks project via `<Compile Include="..." Link="..." />`
  (not `dotnet/sourcelink`, which is the unrelated PDB-to-source
  feature) so build-time validation and runtime parsing compile
  the same source. Today's
  `instructions-files-metadata-generator.ts` (TS) is retired; the
  .NET task replaces it as the single producer.
- `applyTo` parser: comma-split, brace-expand `{a,b,c}` groups,
  trim whitespace, extract extension set. Round-trip invariant
  (`recomposed == original` modulo whitespace) checked per
  corpus file at build time; a failing round-trip fails the build.
- `mcp-tools-registry.json` renamed from `mcp-workers-registry.json`
  (today's path at `src/AutoContext.Mcp.Server/mcp-workers-registry.json`).
  Schema renamed alongside. Both move under
  `src/AutoContext.Engine/Resources/` so the binary host owns every
  manifest it ships.
- Build-time projection of `mcp-tools.json` (wire shape only;
  runtime projection applies the disabled-state filter) emitted into
  `src/AutoContext.Engine/Resources/`.

**Tests**:
- Builder round-trips for every file in the corpus.
- Wire/internal split: `instructions-files.json` round-trips against
  the `Instructions.List` envelope (test asserts equality);
  `instructions-files-metadata.json` carries section maps and parsed
  `applyTo` extension sets and never leaks onto the wire.
- `applyTo` parser fixtures: comma lists, brace expansion, nested
  globs (`**/*.{cs,fs,vb}` → three globs), idempotence
  (parse∘compose ≈ identity).
- A `applyTo` value that would silently canonicalise (e.g. `**` vs.
  `**/*`) is preserved verbatim; the parser refuses to "simplify".
- `mcp-tools-registry.json` schema-validates at build time;
  malformed registry fails the build.

**Out of scope**: any runtime projection (Phase 6); the
content-search index seed (Phase 6 uses it but builds the live
index in-memory at startup).

## Phase 6 — Instructions corpus runtime + projection

**Goal**: engine answers every `Instructions.*` RPC from in-memory
snapshots, applies per-request projection (disabled rules filtered,
`[INSTxxxx]` stripped, overrides resolved), invalidates cleanly via
`Config.Subscribe`, and exposes content search.

**Design anchors**: `§ RPC surface` (`Instructions.*`),
`§ P2` (discriminated envelopes), `§ P9` (snapshot-immutable),
`§ alwaysAttached pitfall`, `§ Instructions.Get distinguishes disabled
from not-found pitfall`, `§ Override survival across upgrades`
pitfall.

**Code touch**:
- `AutoContext.Engine.Core/Instructions/`:
  - `InstructionsCorpusService` — load on startup from the embedded
    side-cars, hold the immutable snapshot, re-project per request.
  - `InstructionsFileBodyProjector` — disabled-rule filter,
    `[INSTxxxx]` tag strip, override resolution.
  - `InstructionsContentIndex` — in-memory content search seeded
    from `instructions-files-metadata.json`, hot across queries,
    invalidated on corpus reload.
  - `InstructionsOverrideWatcher` — `FileSystemWatcher` on
    `<workspace>/.github/instructions/` with the same debounce shape
    Phase 3 introduced.
- Handlers: `Instructions.List`, `Get`, `GetAll`, `GetAlwaysAttached`,
  `GetRaw` (with `opts.source: "bundled"|"override"|"active"`),
  `SearchContent`, `Subscribe`. Discriminated envelopes per `§ P2`.
- `Config.Subscribe` consumer that re-evaluates `disabled` flags and
  rebroadcasts on `Instructions.Subscribe`.
- Override-mtime-vs-bundled-mtime warning (the *override survival*
  pitfall).

**Tests**:
- `List` returns every bundled + override file; disabled rows carry
  `disabled: true`; `alwaysAttached` flag correctly reflects YAML
  frontmatter.
- `Get` discriminated envelope: `ok` (with projected body),
  `disabled` (identity only — no description, no body, no version),
  `not-found` (just the name).
- `GetAll` filters disabled files unconditionally.
- `GetAlwaysAttached` returns only files with `alwaysAttached: true`
  in deterministic order; disabled always-attached files are
  omitted (`GetAlwaysAttached` never returns a `disabled`-envelope
  identity).
- `GetRaw` with each `source` value: `active` resolves
  override-over-bundled, `bundled` and `override` are explicit and
  preserve byte alignment with the source file (critical for the
  CodeLens use case).
- `SearchContent` returns scored hits with anchors; disabled files
  excluded by default; `opts.includeDisabled` flips them in.
- Snapshot immutability across a concurrent corpus reload: a reader
  iterating `GetAll` sees no torn state.
- `Config.ToggleFile` / `Config.ToggleRule` -> `Instructions.Subscribe`
  fan-out re-evaluates the `disabled` flag without a corpus reload.
- Override mtime older than bundled mtime emits a warning event.

**Out of scope**: LM-tool shims (Phase 14 — they dial these RPCs);
MCP-tool dispatch (Phase 7).

## Phase 7 — MCP tool catalogue, dispatch, and worker manager

**Goal**: engine absorbs today's `AutoContext.Mcp.Server` worker
dispatcher. `McpTools.List` and `McpTools.Invoke` answer over the
`rpc` pipe; the MCP-server-only role over stdio comes in Phase 11.
Workers are spawned by the engine via the same lazy
`ensureRunning(workerId)` pattern in use today.

**Design anchors**: `§ RPC surface` (`McpTools.*`), `§ Resource
manifests` (`workers.json`, `mcp-tools-registry.json`),
`§ McpTools.Invoke and MCP tools/call share one handler` pitfall,
`§ What the engine absorbs from today's topology`.

**Code touch**:
- `AutoContext.Engine.Core/Workers/WorkerManager` — port of
  today's `WorkerManager` from `AutoContext.Mcp.Server/Workers/`
  into the engine library. `ensureRunning(workerId)` gate unchanged.
- `Resources/workers.json` build generator — scans
  `src/AutoContext.Worker.*/` projects, derives `id`, `type`,
  `entrypoint`. Id-collision fails the build.
- `McpTools.List` handler over the `mcp-tools-registry.json` data,
  filtered per-request by `disabledTools`/`disabledTasks` from the
  config snapshot.
- `McpTools.Invoke` handler: schema-validate `arguments` against the
  tool's `inputSchema`, dispatch to the worker, marshal the worker
  response into the discriminated envelope (`ok`/`tool-error`/
  `schema-error`/`disabled`/`not-found`). Cancellation forwards
  through the existing `IMcpTask` token.
- Cross-process worker pipes stay on the existing worker-control
  contract (now living in `AutoContext.Framework.Workers` after the
  Phase 0 consolidation; workers themselves are not absorbed).

**Tests**:
- `McpTools.List` reflects the registry, filtered by disabled state
  from `Config.Get`; toggling config fans out via
  `Config.Subscribe` and a subsequent `List` reflects the change.
- `McpTools.Invoke` happy path: dispatched to the right worker per
  the registry's `endpoint` field; response composed into the wire
  envelope; `content` block-for-block matches the worker payload
  (P1 — same shape regardless of transport).
- `schema-error` on malformed `arguments`.
- `tool-error` on a worker reporting failure.
- `disabled` / `not-found` envelopes carry identity only, not the
  schema or description.
- Cancellation: caller cancels mid-invoke; worker's `IMcpTask`
  observes the token and returns the cancellation envelope cleanly.
- `WorkerManager` `ensureRunning` semantics: two concurrent
  `Invoke`s against the same worker race once into one spawn.

**Out of scope**: MCP-server-only role over stdio (Phase 11 — it
reuses these same handlers).

## Phase 8 — Worker → engine logging integration

**Goal**: every `ILogger<T>` record a worker emits ships via
`Engine.WriteLog` to the engine, gets routed by `category` prefix to
the right `worker-<workerId>.log`, fans out on `logs` and
`Logs.Tail*`, with bounded ring buffering and stderr fallback when
the engine is briefly unreachable.

**Design anchors**: `§ RPC surface` (`Engine.WriteLog`, `Logs.GetWorker`,
`Logs.TailWorker`), `§ Log pipeline backpressure` pitfall,
`§ Worker–engine connectivity` pitfall, the *Log categories* table.

**Code touch**:
- `AutoContext.Framework.Logging/AddEngineLoggerProvider` — new
  `ILoggerProvider` that wraps `ILogger<T>` records into the
  canonical envelope, dials the engine's `rpc` pipe for
  `Engine.WriteLog` notifications. (Lives in `Framework.Logging`
  rather than a separate `Worker.Shared` after the Phase 0
  consolidation.)
- Worker-side bounded in-memory ring (default 1000 records / 1 MiB,
  drop-oldest on overflow), retry with exponential backoff, replay
  on reconnect. On drop, one line to **stderr** per drop batch
  (`engine log dropped N records`).
- Engine-side `Engine.WriteLog` handler routes by `category` prefix
  (`worker.<workerId>.*` → `worker-<workerId>.log`; everything else →
  `engine.log`). Per-worker file created lazily on first record.
- Engine supervises worker stderr via `Process.Start` and emits each
  captured stderr line under category
  `worker.<workerId>.engine.stderr`, landing in the right per-worker
  file by the prefix rule.
- `Logs.GetWorker` / `Logs.TailWorker` handlers — `not-found`
  discriminated envelope distinguishes "this `workerId` was never
  spawned" from empty `records`.

**Tests**:
- Worker `ILogger<T>` record arrives in the right per-worker log
  file under the expected category prefix.
- Engine-side fan-out: a subscriber on `logs` sees both engine and
  worker records with the same envelope.
- `Logs.GetWorker("never-spawned")` returns `not-found`;
  `Logs.GetWorker("spawned-but-quiet")` returns `ok` with empty
  `records`.
- Worker can't reach engine on cold start: records buffer into the
  ring; on engine availability the ring drains in order.
- Ring overflow: oldest records drop; one stderr line per batch;
  next successful drain carries a synthetic
  `engine.logging`/`warning` "dropped N worker log records" record.
- Slow `Logs.Tail*` subscriber is evicted with the terminal
  `evicted` frame; the file sink and other subscribers keep going.
- Worker stderr (a print that bypasses the logger) shows up under
  `worker.<id>.engine.stderr` in the worker's log file.

**Out of scope**: any on-disk worker spool — there isn't one, by
design.

## Phase 9 — Discovery

**Goal**: engine builds the *category → MCP tool* and *extension →
instruction file* indices from already-owned state and answers
`Discovery.RouteForPrompt` / `Discovery.RouteForTool`. The `.cjs`
hooks (Phase 15) stop carrying their own scan logic.

**Design anchors**: `§ RPC surface` (`Discovery.*`), `§ P7`.

**Code touch**:
- `AutoContext.Engine.Core/Discovery/DiscoveryService` — two
  indices, rebuilt on `Instructions.Subscribe` / `McpTools.List`
  changes, filtered by current disabled state.
- Word-boundary literal scan for categories;
  `\.[A-Za-z][A-Za-z0-9]{0,12}` regex for extensions — same shape
  as today's `.cjs`.
- `RouteForPrompt(prompt)` and `RouteForTool(toolName)` handlers.

**Tests**:
- Routing fixtures from the existing hook tests, ported to .NET.
- Disabled tools / files don't appear in the result set.
- Index rebuilds on `Instructions.Subscribe` / config change without
  a corpus reload.

**Out of scope**: hook integration (Phase 15).

## Phase 10 — Agent.* RPCs

**Goal**: engine accepts the agent-loop notifications hooks fire
(`SubagentStarted`/`SubagentStopped`/`Compacted`/`ToolUsed`/`TurnEnded`)
and re-broadcasts them on `Agent.Events.Subscribe`. UX-only;
fire-and-forget; lost events tolerable (per the design).

**Design anchors**: `§ RPC surface` (`Agent.*`), `§ P6` (subscription
shape), `§ P10` (cross-process fan-out).

**Code touch**:
- `AutoContext.Engine.Core/Agent/AgentEventsBroadcaster` — same
  per-subscriber bounded-buffer / slow-subscriber-eviction discipline
  Phase 2 introduced.
- The five notification handlers; in-memory per-session histogram for
  `ToolUsed` (consumed by `Diagnostics.Run` in a later out-of-scope
  release).

**Tests**:
- Notification → broadcast round-trip per event family.
- Slow subscriber on `Agent.Events.Subscribe` is evicted; producer
  is never back-pressured.
- Two clients subscribed concurrently see the same envelope sequence.

**Out of scope**: hook script integration (Phase 15);
`Diagnostics.Run` consumer.

## Phase 11 — MCP-server-only role

**Goal**: `autocontext-engine --mcp-server with-stdio` runs the
minimal stdio MCP server. No pipes, no registry entry, no
`engine.log`, no `FileSystemWatcher`, no worker dispatch.
Per-request disk read of `.autocontext.json`. Stdio EOF exits
cleanly.

**Design anchors**: `§ Engine binary` (role split),
`§ Engine options (CLI surface)` (MCP-server argv subset),
`§ Lifecycle` (*MCP-server-only role is out of scope*),
`§ MCP-server role argv discipline` pitfall.

**Code touch**:
- `AutoContext.Engine/Program.cs` — argv parser splits on
  `--mcp-server` and routes into one of two disjoint
  `IHostBuilder` compositions. MCP-only branch registers
  `AddMcpServer().WithStdioServerTransport()` and **nothing else
  state-bearing**.
- Argv parser rejects `--instance-id`, `--instance-label`,
  `--idle-timeout`, `--parent-pid`, `--retention`, `--logging` in
  the MCP-only role with a stderr error and non-zero exit.
- The same handler code from Phase 6 (`Instructions.*`) and Phase 7
  (`McpTools.*`) is registered as `instructions_*` and the existing
  `analyze_*` / `read_*` MCP tools (today's surface). The per-request
  `.autocontext.json` read is wired into the handler dependency
  graph for this role.
- `AutoContext.Mcp.Server/Program.cs` shrinks to a thin shim that
  delegates to the engine binary's MCP-server-only role — kept only
  for the in-tree smoke test that still spawns it; deleted in
  Phase 16.

**Tests**:
- Stdio mode rejects each daemon-only switch.
- Stdio mode does not bind any pipe (assert with a parallel daemon
  on the same workspace — they coexist).
- `tools/list` and `tools/call` return byte-identical `content` for
  the same input as the pipe `McpTools.Invoke` (P1 cross-transport
  diff test).
- Per-request disk re-read: a write to `.autocontext.json` from a
  parallel daemon is observed on the next stdio request.
- Stdio EOF exits cleanly.
- No `engine-registry.json` entry written.

**Out of scope**: deleting `AutoContext.Mcp.Server` (Phase 16);
extension's MCP server definition repointing (Phase 14).

## Phase 12 — `Client.Core` (CLI-as-library) and `EngineDaemonManager` (TS)

**Goal**: two independent deliverables that happen to land together
because both first need the engine's wire surface from Phases 1–11.
They are **not** parallel implementations of one concept — they
have different responsibilities, different consumers, and only
share the engine's wire contract.

- `AutoContext.Client.Core` (.NET) — the `autocontext` CLI as a
  library. **Created in this phase** — first consumer is the typed
  RPC surface this phase introduces — alongside its sibling test
  project `AutoContext.Client.Core.Tests` (both added to
  `AutoContext.slnx` and `build.ps1`). Houses every type the CLI
  binary uses internally (`EngineClient` typed-RPC surface,
  four-pipe dialer, cold-start-or-attach resolver, subscription
  consumers, `IEngineSpawner`). References `Framework.Pipes` +
  `Framework.Logging` + `Framework.Protocol`. Consumers:
  `AutoContext.CommandLine` and third-party .NET embedders that
  want CLI-shaped behaviour in-process. See
  [`autocontext-cli.md`](future/autocontext-cli.md) for the
  full CLI-as-library picture.
- `EngineDaemonManager` (TS, `src/AutoContext.Nodejs.Core/src/engine/`) —
  owns engine-daemon lifecycle on the TS host side (find-or-spawn
  against the bundled `autocontext-engine` binary, supervise the
  child, tear down on host shutdown) **and** exposes the engine's
  RPC surface as typed methods on top of that lifecycle. Consumers:
  the VS Code extension (Phase 14) and the agent-plugin `.cjs`
  hook scripts (Phase 15).

**Design anchors**: `§ Composition contracts`, `§ Sharing principle`,
`§ Lifecycle` (cold start, warm reuse), `§ P6`/`§ P9`/`§ P10`.

**Code touch**:
- `AutoContext.Client.Core/`:
  - Pipe dialer (`rpc` + `events` only by default; `health` and
    `logs` opt-in).
  - Find-or-spawn flow over a `IEngineSpawner` seam (concrete
    impl: `Process.Start` against the bundled binary). Cold-spawn
    retry with the doc's connect-budget shape.
  - `Engine.Hello` handshake; refusal surfaces as a typed
    `EngineProtocolException`.
  - Typed RPC clients for every surface: `Config.*`, `Instructions.*`,
    `Workspace.*`, `McpTools.*`, `Discovery.*`, `Agent.*`, `Logs.*`,
    `Engine.Lifecycle`, `Engine.ListRegistryEntries`,
    `Engine.Shutdown`. Note: `Engine.WriteLog` is **not** exposed
    on `Client.Core`'s typed surface — it is a worker→engine
    notification owned by `Framework.Logging`
    (`EngineWriteLogClient` + `AddEngineLoggerProvider`); the wire
    DTO itself lives in `Framework.Protocol` so both sides marshal
    the same envelope.
  - Subscription consumers as `IAsyncEnumerable<T>` (P6 — first-class
    subscriptions; P8 — async I/O end-to-end).
- `AutoContext.Nodejs.Core/src/engine/engine-daemon-manager.ts` —
  plain TS class `EngineDaemonManager` (no DI container per
  `§ Sharing principle`). Same wire surface, same RPC names, same
  envelope shapes; additionally owns find-or-spawn against the
  bundled `autocontext-engine` binary and supervises the child
  process for the lifetime of the host.
- TS pipe-client substrate lives in
  `AutoContext.Nodejs.Core/src/pipes/` (today's location
  `AutoContext.Framework.Web/src/pipes/`, moved as part of the
  Phase 0 rename); extended where the four-pipe shape needs it.

**Tests**:
- .NET client round-trips every RPC against an in-process engine
  composed via `AddAutoContextEngine`.
- TS client round-trips every RPC against a spawned engine binary
  (Vitest + `child_process`).
- Cold spawn: client connects, no engine present, spawner fires,
  client retries within the connect budget, handshake succeeds.
- Warm reuse: two clients of the same launcher (same `--instance-id`)
  see one engine.
- `*.Subscribe` streams: snapshot-on-subscribe, revision counter,
  late-subscriber correctness.
- Slow-subscriber on the client side disconnects with `evicted`
  rather than back-pressuring the engine.
- Engine refusal on protocol-version mismatch surfaces as a typed
  error on both clients.

**Out of scope**: extension consuming the client (Phase 14); hooks
consuming the client (Phase 15); CLI verb implementations
(`autocontext-cli.md`, separate plan).

## Phase 13 — Distribution and packaging

**Goal**: `build.ps1 Package` emits per-RID engine staging under
`out/engine/<rid>/...`; per-platform packaging (VSIX, plugin
release, GitHub-release tarball) selects the matching RID and
copies the flat `engine/` subtree into the shipped artefact. The
engine resolves its side-cars from `AppContext.BaseDirectory`
without any host-supplied path.

**Design anchors**: `§ Distribution`, `§ Distributed bundle layout`,
the per-platform packaging note (`vsce package --target <target>`).

**Code touch**:
- `build.ps1` — new actions for per-RID engine publish
  (`dotnet publish -r <rid> --self-contained`), per-worker
  self-contained publish into `Workers/<id>/`, manifest copy
  (`Instructions/`, `Resources/`), per-platform packaging that
  selects one RID's staging into one VSIX / one plugin release /
  one tarball.
- `package.json` (`AutoContext.VsCode/`) — `vsce package` invocation
  shifts to `--target <target>` per supported platform; the
  `engine/` directory replaces today's per-RID layout under the
  extension root.
- Engine-side `AppContext.BaseDirectory` resolver for `engine/`
  side-cars; no host-supplied root for resource resolution.
- Workers move into per-worker subdirs (`Workers/workspace/`,
  `Workers/dotnet/`, `Workers/web/`) to isolate self-contained
  runtimes from each other and from the engine.

**Tests**:
- `build.ps1 Package -Local` per RID succeeds.
- A packaged engine binary started inside its staging dir resolves
  every side-car (manifest fixture for each).
- Per-platform VSIX contains the right RID's binaries and no others
  (size + spot-check assertions).
- Plugin release for each platform mirrors the same layout.
- Corpus byte-equality across RIDs in one build (manifest fixture).
- GitHub-release tarball smoke (build, extract, run `--version`).

**Out of scope**: marketplace publishing (separate operational
step); existing extension still ships its TS-side instruction
artefacts until Phase 14.

## Phase 14 — Extension migration

**Goal**: extension becomes a pure `EngineDaemonManager` consumer. The
sideband pipe servers and the in-extension projection/config/corpus
classes are deleted. Tree views, decoration providers, CodeLens, and
LM tools dial the engine over the four pipes.

**Design anchors**: `§ Authority model: engine owns, clients cache`,
`§ Projection ownership`, `§ Sharing principle`, `§ LM-tool surface`.

**Code touch — deletions** (from `src/AutoContext.VsCode/src/`):
- `autocontext-config-manager.ts`, `autocontext-config-projector.ts`,
  `autocontext-config-server.ts` — replaced by `Config.*` RPCs.
- `instructions-file-content-projector.ts`,
  `instructions-files-manager.ts`,
  `instructions-files-manifest-loader.ts`,
  `instructions-files-manifest.ts`,
  `instructions-files-metadata-generator.ts`,
  `instructions-files-metadata-loader.ts`,
  `instructions-files-override-watcher.ts`,
  `instructions-file-parser.ts`,
  `instructions-file-sections-cache.ts`,
  `instructions-file-sections-parser.ts` — replaced by
  `Instructions.*` RPCs and the engine-side corpus from Phase 6.
- `log-server.ts`, `health-monitor-server.ts`,
  `worker-control-server.ts` — replaced by engine pipes.
- `workspace-context-detector.ts` — replaced by `Workspace.Detect`.
- `worker-manager.ts` — engine spawns workers itself; the extension
  spawns the engine (and only the engine).
- The four LM-tool handler implementations
  (`instructions-files-lm-tools-*`) collapse to thin shims that dial
  `Instructions.*` over `EngineDaemonManager`.

**Code touch — additions/changes**:
- `extension-activation.ts` / `extension-composition.ts`: spawn the
  engine with `--workspace <path> --instance-id <uuid> --idle-timeout
  0 --parent-pid <vscode-pid> --instance-label "vscode (v…); engine
  (v…)"`. The instance UUID is minted once per window.
- Tree views, decoration providers, CodeLens, and hover providers
  all read from `EngineDaemonManager`. `Engine.Lifecycle.Subscribe`,
  `Config.Subscribe`, and `Instructions.Subscribe` drive cache
  invalidation in the UI.
- The MCP server definition (`mcp-server-provider.ts`) repoints to
  `autocontext-engine --mcp-server with-stdio`. Today's
  `--endpoint-suffix` side-channel from the extension's launcher to
  the MCP-host's spawn is replaced wholesale by `--instance-id`.
- `agent-plugin-installer.ts` keeps installing the hook scripts, but
  the hooks now dial the engine (Phase 15).

**Tests**:
- Extension Vitest suites: every replaced module's test coverage
  migrates onto `EngineDaemonManager` fakes / engine-in-process fixtures.
  No coverage drops below the replaced module's bar.
- `build.ps1 Test -Smoke` (the VS Code extension smoke test) runs
  end-to-end: extension activates, spawns the engine, tree view
  populates, an instruction toggle round-trips.
- Cross-window scenario: two VS Code windows on the same workspace
  spawn two engines; toggles in one window reach the other through
  the cross-instance `.autocontext.json` path (Phase 3 contract).

**Out of scope**: hook scripts (Phase 15); `Mcp.Server` deletion
(Phase 16).

## Phase 15 — Agent-plugin hook migration

**Goal**: the agent-plugin hooks (today's `.cjs` scripts under
`src/AutoContext.VsCode/plugin/hooks/`) call `EngineDaemonManager` for
everything. SessionStart, UserPromptSubmit, PreCompact, and the
SubagentStart/Stop pair land in this phase; PreToolUse / PostToolUse
/ Stop land too because they share the same client and the same RPC
families.

**Design anchors**: `§ Topology — motivating clients` (agent
plugin), `§ RPC surface` (`Agent.*`, `Discovery.*`,
`Instructions.GetAlwaysAttached`).

**Code touch**:
- Hook scripts move from "carries its own routing scan + corpus
  reader" to "calls `Instructions.GetAlwaysAttached`,
  `Discovery.RouteForPrompt`, `Discovery.RouteForTool`, and fires
  the `Agent.*` notifications". The TS `EngineDaemonManager` from
  Phase 12 is the only seam.
- Sub-agent file materialisation under the per-instance cache root
  (`%LOCALAPPDATA%\autocontext\<workspaceHash>\<instanceId>\cache\subagents\<sessionId>\`,
  POSIX equivalent) lives in the SubagentStart hook; SubagentStop
  cleans it.
- `--instance-id` propagation: hook templates document the
  side-channel the launcher provides (env var inherited from
  launcher); a hook with no resolvable instance-id spawns its own
  engine per the design's *Hook scripts outside a known launcher*
  pitfall.
- The `Engine.Hello`-failure path is a structured hook failure (no
  in-hook disk-read fallback; engine + plugin ship versioned
  together).

**Tests**:
- Per-hook fixture-based tests against a spawned engine.
- Side-channel UUID inheritance: hook with env var reaches the
  launcher's engine; hook without spawns its own.
- Sub-agent cache materialisation + cleanup.
- `Engine.Hello` mismatch surfaces as a structured hook error.

**Out of scope**: any host-specific hook-host detection (the design
says hooks are host-agnostic — Claude Code, VS Code Copilot, future
hosts).

## Phase 16 — `AutoContext.Mcp.Server` retirement

**Goal**: the standalone MCP-server project is gone. The MCP host
servers manifest (`servers.json`) points at
`autocontext-engine --mcp-server with-stdio`. Tests fold into
`AutoContext.Engine.Core.Tests`.

**Design anchors**: `§ What the engine absorbs from today's topology`,
`§ Test-project layout`.

**Code touch**:
- Delete `src/AutoContext.Mcp.Server/` and
  `src/tests/AutoContext.Mcp.Server.Tests/`.
- Tests worth keeping move into `AutoContext.Engine.Core.Tests`
  (the schema-validation tests, the manifest-loader tests, the
  envelope-composition tests).
- `servers.json` rewritten: the only entry is `autocontext-engine`
  with the MCP-server-only role argv; worker entries are removed
  (the engine spawns workers itself now).
- The `mcp-tools-registry.json` move into
  `AutoContext.Engine/Resources/` already happened in Phase 5;
  deleting `AutoContext.Mcp.Server/` here removes anything left in
  its directory by definition.
- Solution file (`AutoContext.slnx`) cleaned of the retired project.
- `build.ps1` no longer references `AutoContext.Mcp.Server`.

**Tests**:
- Full solution build + test + smoke green.
- A spawned `autocontext-engine --mcp-server with-stdio` answers
  every `tools/list` and `tools/call` the old `Mcp.Server` answered
  (regression fixture set lifted from `AutoContext.Mcp.Server.Tests`).

**Out of scope**: any further surface work; the engine has shipped.

## Cross-phase concerns

### Risk and ordering

- **Phase 3 (config) and Phase 6 (instructions runtime) are the
  highest-risk phases.** Reload coalescing and snapshot immutability
  are subtle; both ship with the heaviest test budget.
- **Phase 13 (distribution) cannot ship before Phase 11
  (MCP-server-only role).** The shipped binary needs to support both
  roles before any host bundle includes it.
- **Phase 14 (extension) cannot ship before Phases 6, 7, 9, 12.**
  The extension consumes every one of those surfaces.
- **Phase 15 (hooks) cannot ship before Phase 12 (TS client).**
- **Phase 16 (Mcp.Server retirement) is last** so the regression
  surface stays observable until everything else has flipped.

### What every phase explicitly does *not* do

- No version bumps. `version.json`, `package.json` `version` fields,
  and `.csproj` `<VersionPrefix>` stay where they are until the user
  asks (see `copilot-instructions.md` § Versioning).
- No instruction-corpus content edits unless the phase explicitly
  rewrites a section the engine is replacing. Engine consolidation
  does not mean rewriting curated guidance.
- No "improvements" beyond the phase scope. Refactors, style
  sweeps, doc cleanups, or unrelated bug fixes wait for their own
  change (see `copilot-instructions.md` § Implementation Discipline).
- No new portability abstractions (`IFileSystem`, `IWorkspace`,
  …) — see `design § Sharing principle` and its pitfall entry.

### Resolved pre-flight decisions

1. **Phase 4 flag table — port the existing TS tables verbatim.**
   The authoritative rule set already lives declaratively in
   [`workspace-context-detector.ts`](../src/AutoContext.VsCode/src/workspace-context-detector.ts)
   as four `as const` arrays: `fileRules` (file-glob flags),
   `npmContentRules` (`package.json` regex flags),
   `dotnetContentRules` (`.csproj` regex flags), and
   `flagActivationRules` (`[child, parent]` transitive activations).
   Phase 4 ports each table to C# unchanged — same flag names, same
   globs, same regex patterns, same activation edges. The ~60-flag
   contract in the design doc and these tables are the same set; no
   separate fixture extraction is needed. The per-flag tests use the
   existing
   [`workspace-context-detector.test.ts`](../src/AutoContext.VsCode/tests/unit-tests/workspace-context-detector.test.ts)
   fixtures as their porting source.
2. **Cache path migration — sweep deletes orphans.** The
   per-instance cache root has progressed through two preview
   shapes: bare `<workspaceHash>` (earliest) and flat
   `<workspaceHash>#<instanceId>` (intermediate). The shipped
   shape is nested `<workspaceHash>\<instanceId>\` (POSIX: `/`).
   The Phase 2 housekeeping sweep treats any subtree under the
   engine's cache root that does not match the canonical nested
   shape — or matches the shape but has no live `engine-registry.json`
   entry — as stale, and deletes it once it falls outside the
   retention floor (this is the `Foreign` arm of
   `SubtreeRegistryStatus` for the two legacy shapes, and the
   `StaleRegistration` / `Unregistered` arms for canonical-shape
   orphans). The
   cache root is engine-owned (P5), nothing else writes there, and
   cache contents are reproducible, so a stale orphan is just disk
   pressure. The retention floor protects against deleting a
   directory that another engine is mid-write into.
3. **`Workspace.Detect` arbitrary-path RPC — no action needed.**
   Audit of every `workspaceContextDetector` consumer
   (`auto-configurer.ts`, `extension-composition.ts`,
   `extension-activation.ts`, `extension-registrations.ts`,
   `instructions-viewer-code-lens-provider.ts`) confirms every call
   is the parameterless `detector.detect()` that scans the active
   workspace. No caller passes an arbitrary path. The design's
   "engine's `--workspace` only" rule is already what today's
   extension does; Phase 14 simply replaces `detector.detect()`
   with `client.Workspace.Detect()`.

## Companion documents

- [`future/autocontext-engine.md`](./future/autocontext-engine.md)
  — design authority.
- [`future/autocontext-cli.md`](./future/autocontext-cli.md) — CLI
  subcommands plan, separate from this rollout.
- `architecture-centralized-mcp.md` (repo memory under
  `/memories/repo/`) — current-topology context; provides the project
  layout and naming conventions every phase keeps consistent with.
