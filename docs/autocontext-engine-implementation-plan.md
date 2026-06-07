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
  `Worker.*` shape for new worker projects and the `Framework.Tests.Support`
  shape for shared .NET test-support code, the
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
    — `instructionsManifestSnapshot` over `corpus`, `pendingReload`
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
  bump, every subscriber attach / drop, and every error path — at
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
- **One branch per phase, named for the deliverable, not the phase
  number.** Use `features/<kebab-case-deliverable>` (e.g.
  `features/framework-restructure`,
  `features/engine-lifecycle-substrate`) — never
  `phase/NN-anything`. Phase numbers are a today-only coordinate
  that drifts when phases are split, merged, or reordered; the
  branch name should still make sense a year later when someone is
  reading `git log` cold. One phase = one branch = one PR, off the
  previous phase's merge commit (or off `main` when the phases are
  genuinely independent).
- **Several focused commits per branch; no squash on merge.** A
  phase rolls in as a sequence of small, behaviourally-coherent
  commits (one per logical sub-step — e.g. project split, shim
  removal, registration, rename), not one mega-commit. Hard rule:
  every individual commit compiles green and passes its tests via
  `.\build.ps1 Prepare`; if a split can't satisfy that, the split
  is wrong, not the rule. Merge the PR with a merge commit or
  "Rebase and merge", never "Squash and merge" — squashing destroys
  the per-step bisect granularity that justified the focused
  commits in the first place. PR title mirrors the branch's
  deliverable as a Conventional Commits sentence (e.g.
  `refactor(framework): split into pipes/logging/protocol/workers`).
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
- **P3**: three decoupled representations — on-disk (authoring /
  generation), engine-internal (runtime snapshot), and wire (per-RPC
  projection); none dictates another's shape. On disk the curatorial
  layer is hand-authored (`instructions-catalog.json`) and the
  per-file facts are build-generated (`instructions-manifest.json`).
- **P4**: workspace identity is one hash; engine identity adds one
  per-launch UUID (fresh on every spawn; never reused across
  respawns). Endpoint names use the flat `<workspaceHash>#<instanceId>`
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
  with slow-subscriber drop on every `*.Subscribe`.
- **P10**: in-process async hooks are single-subscriber; cross-process
  fan-out is `*.Subscribe`. No classic .NET `event` slots in framework
  code.
- **P11**: the engine library splits into a **capability tier** and an
  **infrastructure substrate**. `Engine.Core/Features/` holds the
  outward-facing capabilities the extension consumes over RPC
  (`Instructions/` today; the `McpTools.*` capability is the next
  tenant) — the engine still boots without any of them, but without
  them nothing can consume anything. Everything outside `Features/` is
  infrastructure: required for the engine to run, whether a substantial
  subsystem (`Workspace/`, `Lifecycle/`, `Machine/`) or plumbing
  (`Watchdogs/`, `Logging/`, `Infrastructure/`). A folder earns the
  `Features/` tier only when (a) the engine still runs without it *and*
  (b) it directly serves an outward consumer; `Workspace/` and the
  engine registry fail (b) and stay at root. The dependency arrow is
  one-way: capabilities may depend on the substrate, the substrate must
  never depend on a capability — a `using …Features.*` inside
  infrastructure is the smell that classification slipped.

Anything that adds an interface "for portability" needs a second
concrete implementation in the same phase or it doesn't ship. See
`design § Sharing principle`. Test fakes count as a second implementation
when the seam exists specifically to make the production path testable
(e.g. spawn-by-process vs. spawn-in-test); abstractions added for any
other reason still need a real second impl.

## Test strategy (applies to every phase)

- **Unit tests** run against the engine library composed in-process
  via `AddAutoContextEngine(...)` with a per-test workspace path and
  an overridden endpoint prefix (library-only `EngineOptions` knob
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
  `AutoContext.Engine.Protocol.Tests`,
  `AutoContext.Framework.Workers.Tests`,
  `AutoContext.Engine.Core.Tests` (absorbs today's
  `AutoContext.Mcp.Server.Tests` over the course of phases 7 and 16),
  `AutoContext.Client.Core.Tests`, `AutoContext.Engine.Tests`,
  `AutoContext.Instructions.Parser.Tests` (frontmatter + `applyTo`
  parser fixtures, round-trip invariant, section-index and cross-file
  reference-resolution coverage) and
  `AutoContext.Instructions.Manifest.Generator.Tests` (manifest
  builder + serializer assertions; the generator is also exercised
  end-to-end by the engine's build).
  Worker test projects are unchanged.
- **TS tests** stay in Vitest, in the same layout
  `AutoContext.Nodejs.Core` and `AutoContext.VsCode` already use.
- **Smoke tests** route through `build.ps1 Compile -Smoke` as they do
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
- `AutoContext.Engine.Protocol/` — cross-side DTOs (the wire
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
  this rollout — see Phase 0; `IMcpTask` and
  `WorkerHostBuilderExtensions` both move into `Framework.Workers/`.
  The new `Engine.WriteLog`-side logger files land in
  `Framework.Logging/` in the engine-rollout phases that introduce
  them, not in Phase 0.)
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
    AddEngineLoggerProvider.cs                 # new in engine rollout — wires the engine-side logger provider
    EngineLoggerProvider.cs                    # new in engine rollout — `ILoggerProvider` that dials Engine.WriteLog
    EngineLogIngestRing.cs                     # new in engine rollout — bounded ring buffering log records
    EngineWriteLogClient.cs                    # new in engine rollout — typed client for the Engine.WriteLog RPC
    # Legacy sideband sink (dragged in Phase 0, deleted in Phase 8 once
    # Engine.WriteLog is the only worker→engine log path):
    PipeLogger.cs
    PipeLoggerProvider.cs
    LoggingClient.cs
    JsonLogGreeting.cs
    LogServerJsonContext.cs

  AutoContext.Engine.Protocol/              # cross-side DTOs + endpoint shapes (leaf — no references)
    AutoContext.Engine.Protocol.csproj
    EndpointKind.cs                            # enum { Rpc, Events, Health, Logs } — the four logical channels per (workspace, launcher instance)
    Endpoint.cs                                # `readonly record struct` implementing IParsable<Endpoint> — builder + parser for rpc/events/health/logs × hash#instance
    ServiceAddressFormatter.cs                 # legacy `autocontext.<role>#<instance-id>` formatter — kept until every current-topology dialer flips to Endpoint (Phase 12); deleted in Phase 16
    ProtocolVersion.cs                         # Engine.Hello version constant
    LogRecord.cs                               # canonical log-record envelope (timestamp, category, level, …)
    Envelopes/                                 # discriminated-envelope base shapes (P2)
      ResultEnvelope.cs                        # ok | disabled | not-found | *-error union root
      OkEnvelope.cs
      DisabledEnvelope.cs
      NotFoundEnvelope.cs
      ErrorEnvelope.cs
    Messages/                                  # per-RPC request/response DTOs
      EngineMessages.cs                        # Engine.Hello / Shutdown / WriteLog
      Lifecycle/                               # Engine.Lifecycle.Subscribe family (events-pipe notification payload)
        LifecycleMethods.cs                    # `Engine.Lifecycle` notification-method constant
        LifecycleEventKinds.cs                 # transition string constants (`started`, `shutting-down`, `reloading`, `dropped`)
        LifecycleEvent.cs                      # notification payload (Kind, InstanceId?, Revision?, Reason?)
      Registry/                                # Engine.RegistryEntries family (request method constant, result DTO, RegistryEntry record)
        RegistryMethods.cs                     # `Engine.RegistryEntries` wire-method constant
        RegistryEntriesResult.cs               # Engine.RegistryEntries response DTO
        RegistryEntry.cs                       # registry entry record (wire shape)
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
      IUniqueInstanceGuard.cs                  # contract for the pre-bind "another engine already owns this <workspaceHash>#<instanceId>?" sanity check; production impl is Lifecycle/PerWorkspaceInstanceGuard.cs
      Storage/                                 # cache-root vocabulary — identity coordinates and path resolution; leaf, consumed by Machine/ (EngineCacheLayout, Housekeeping) and Lifecycle/ (RegistryEntryBuilder), depends on nothing engine-side itself
        CacheRoot.cs                           # per-instance identity bundle — composes EngineOptions into resolved cache-root subtree paths (FullPath / WorkspaceBucketPath / InstancePath / WorkspaceUserPath); the DI singleton every on-disk path resolves through
        CacheRootPathResolver.cs               # pure static — resolves the OS-level engine cache root (%LOCALAPPDATA%\autocontext, $XDG_CACHE_HOME/autocontext, …) with --cache-root override; sole reader of the env vars and override option
        WorkspaceHash.cs                       # 16-uppercase-hex SHA-256 prefix of the workspace path — `readonly record struct` implementing `IParsable<WorkspaceHash>`; the `<workspaceHash>` segment in registry rows and on-disk paths
        InstanceId.cs                          # launcher UUID value type — `readonly record struct` implementing IParsable<T>; the `<instanceId>` segment in endpoint names and on-disk paths (P4)
      Diagnostics/                             # System.Diagnostics.Process seam — internal abstractions used by watchdogs and registry-sweep liveness checks
        IProcessHandle.cs                      # opens-once handle; exposes UTC start time and a cancellable WaitForExitAsync
        IProcessLookup.cs                      # TryOpen(pid) → handle | null (gone / denied); single seam over Process.GetProcessById
        SystemProcessHandle.cs                 # production wrapper over System.Diagnostics.Process
        SystemProcessLookup.cs                 # production lookup; catches ArgumentException / InvalidOperationException / Win32Exception → null
      Events/                                  # fan-out substrate — the generic pub/sub core every stream (Lifecycle, Logs, Config, …) shares; domains add only a thin *StreamFrames framer (and, where they seed/terminate, a thin stream wrapper)
        Broadcaster.cs                         # singleton fan-out core `Broadcaster<TPayload>`: per-subscriber bounded Channel (capacity 64), slow-subscriber drop while the rest keep flowing, graceful Complete (clean EOF, no terminal frame); Subscribe(optional seed) → BroadcasterSubscription<T>
        SnapshotBroadcaster.cs                 # snapshot-on-subscribe wrapper over Broadcaster<T>: caches the latest published payload + Prime(seed) and replays it as the first frame to every new subscriber (post-Complete primes are dropped)
        BroadcasterSubscriber.cs               # per-subscriber bounded channel + Active/Closed/Dropped state machine (Interlocked CAS)
        BroadcasterSubscription.cs             # IDisposable handle returned by Subscribe; ReadAllAsync drains the channel; WasDropped exposes the terminal state the frame stream reads
        BroadcasterLog.cs                      # source-generated slow-subscriber-drop log messages shared by every stream
        IBroadcasterFrameStream.cs             # `IBroadcasterFrameStream<TPayload, TFrame>` — the subscription→frames contract every domain stream satisfies (StreamAsync)
        BroadcasterFrameStream.cs              # abstract base owning the shared drain/terminal-flush skeleton; subclasses supply only ToFrame + CreateDroppedFrame
        TrailingEdgeDebouncer.cs               # capacity-one channel + TimeProvider quiet window (P3 row 4); wrapped by Infrastructure/IO/FileChangeWatcher
    Lifecycle/                                 # this engine's own lifecycle: Hello, Shutdown, own registry entry
      LifecycleService.cs                      # hosted service — owns the four-pipe accept loops
      PerWorkspaceInstanceGuard.cs             # IUniqueInstanceGuard impl — dials the would-be `rpc` endpoint before bind; throws IOException when a live peer answers (P4 launcher-bug guard); not a hosted service
      HelloHandler.cs                          # protocol-version check + greeting payload
      ShutdownHandler.cs                       # graceful drain + Engine.Shutdown RPC
      # — Engine.Lifecycle.Subscribe events stream (P10) — thin domain layer over Infrastructure/Events/ —
      LifecycleEventStream.cs                  # singleton fan-out backing Engine.Lifecycle.Subscribe — wraps a shared Infrastructure/Events/Broadcaster<T>; layers on the `started` seed + terminal-event replay (Subscribe / TryPublish / TryComplete)
      LifecycleFrameStream.cs                  # BroadcasterFrameStream<JsonLifecycleEvent, JsonLifecycleEvent> (IBroadcasterFrameStream impl): drains a BroadcasterSubscription<JsonLifecycleEvent> and yields each event as a wire frame, emitting a terminal `dropped` frame when the subscriber was dropped
      LifecycleNotifier.cs                     # stamps the engine's identity (InstanceId, Revision) onto each transition and publishes through LifecycleEventStream — the stream itself constructs only the seeded `started` event
      # — Engine registry (engine-registry.json mechanics + this engine's own entry) —
      RegistryFileFormat.cs                    # stateless serializer + schema-version contract shared by reader and writer (envelope shape, JsonSerializerOptions)
      RegistryFileReader.cs                    # concurrent-read surface for engine-registry.json (P9 concurrent reads); retry under FileShare.ReadWrite|FileShare.Delete + corrupt-file tolerance (returns empty list)
      RegistryFileWriter.cs                    # internal atomic single-shot writer; temp+fsync+rename only (no mutex, no retry, no RMW — owned by RegistryFileService)
      RegistryFileService.cs                   # hosted coordinator: dedicated worker thread + named cross-process Mutex + Channel<WriteRequest> + read-modify-write cycle; owns this engine's own-entry lifecycle (append on Start, best-effort remove on Stop); single intended caller of RegistryFileWriter
      RegistryEntry.cs                         # entry DTO returned/accepted by RegistryFileReader/Service (engine-internal shape — never on the wire, P3)
      RegistryEntryBuilder.cs                  # pure builder — composes EngineOptions + runtime facts (pid, start time, workspace hash, assembly version) into the RegistryEntry that represents this engine; invoked by RegistryFileService via DI-supplied factory
      RegistryEntryReader.cs                   # composes over RegistryFileReader; applies Process.StartTime peer-liveness check, tagging each entry Live/Stale — consumed by Machine/Housekeeping/ (Phase 2b CacheRootScanner) as the registration half of its classification
    Watchdogs/                                 # process-lifetime guards — peers of Lifecycle/; each is a hosted service that signals IHostApplicationLifetime.StopApplication on its own trigger
      IdleTimeoutWatchdog.cs                   # --idle-timeout
      HostWatchdog.cs                          # --parent-pid; clamps engine lifetime to spawner via Infrastructure/Diagnostics handle (Process.StartTime pid-reuse defeat)
      # NOTE: per-workspace unique-instance guard is NOT a watchdog (one-shot pre-bind probe, not a long-running monitor); see Lifecycle/PerWorkspaceInstanceGuard.cs
    Machine/                                   # engine's on-disk residency: the cache-root subtree this engine owns and the housekeeping that walks the cache root as a whole; consumes Infrastructure/Storage vocabulary, owns no protocol surface of its own
      EngineCacheLayout.cs                     # single source of truth for every on-disk path the engine owns under its cache root (engine.log / crash.log + the shared registry file); composes off the CacheRoot singleton and freezes the resolved paths at construction
      EngineCrashWriter.cs                     # paranoid last-gasp writer of crash.log — sync File.AppendAllText, no DI, no ILogger, no async, allocation-light; wired into DaemonHostFactory.RunAsync top-level try/catch + AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException; never invoked from graceful shutdown paths
      Housekeeping/                            # cache-root upkeep: peer-registration liveness, orphan reaping, retention, foreign-subtree eviction (P5)
        HousekeepingService.cs                 # hosted service — shutdown sweep only, runs after LifecycleService removes own entry + closes pipes; ≤ 1 s deadline budget
        SubtreeRegistryStatus.cs               # discriminated record hierarchy (Registered | StaleRegistration | Unregistered | Foreign) — P2-shaped contract between scanner, policy, and cleaner
        CacheRootScanner.cs                    # walks the engine cache root, produces SubtreeRegistryStatus per child (pure — no deletion here)
        StaleSubtreeCleaner.cs                 # pattern-matches SubtreeRegistryStatus, deletes with concurrent-sweep tolerance (DirectoryNotFoundException counts as success)
        RetentionPolicy.cs                     # single reader of `--retention` — resolves the window per SubtreeRegistryStatus arm (per-entry, unregistered-fallback, foreign)
    Logging/                                   # engine sink, rotation, rotated-file cleanup
      LogChannel.cs                            # single-channel ingest; TryWrite / ReadAllAsync / Complete
      LogFileSinkService.cs                    # drain loop + dispatcher; owns the per-target file appenders (engine.log / worker-<id>.log); from row 5 also fans drained records out through a shared Infrastructure/Events/Broadcaster<JsonLogRecord> (pure live tail)
      LogRotator.cs                            # --logging thresholds (normal vs debug)
      RotatedLogCleaner.cs                     # deletes rotated log files past retention inside a live subtree (uses RetentionPolicy from Machine/Housekeeping/)
      WorkerLogRouter.cs                       # routes Engine.WriteLog by category prefix
      LogFrameStream.cs                        # BroadcasterFrameStream<JsonLogRecord, JsonLogStreamFrame> (IBroadcasterFrameStream impl) for Logs.Tail*: drains a BroadcasterSubscription<JsonLogRecord> (fanned out by LogFileSinkService over the shared Infrastructure/Events/Broadcaster<T>) and yields record/dropped frames
      LogsHandlers.cs                          # Logs.GetEngine / TailEngine / GetWorker / TailWorker
    Workspace/                                 # workspace-scoped state — everything keyed by the current workspace root
      Config/                                  # .autocontext.json owner (Config.* wire surface)
        Snapshot/                              # immutable domain graph (engine-internal source of truth)
          ConfigSnapshot.cs                    # domain: root record + Empty
          ConfigDiagnostic.cs                  # domain: diagnostic prefs record
          ConfigInstructionsFile.cs            # domain: per-instruction-file record (+ nested InstructionsRule)
          ConfigMcpTool.cs                     # domain: per-MCP-tool record (+ nested McpTask)
        Format/                                # on-disk wire DTOs (.autocontext.json shape)
          JsonConfigFile.cs                    # wire DTO: immutable on-disk config shape (P9)
          JsonConfigFileDiagnostic.cs          # wire DTO: diagnostic block
          JsonConfigFileInstructionsEntry.cs   # wire DTO: instructions map entry
          JsonConfigFileMcpToolEntry.cs        # wire DTO: mcpTools object entry
          JsonConfigFileMcpToolValue.cs        # wire DTO: mcpTools value (false | object union)
          JsonConfigFileMcpToolValueConverter.cs # custom converter for the false|object union
        ConfigSnapshotExtensions.cs            # mapper: domain -> on-disk (ToFileFormat) + domain -> Config.* wire (ToWireFormat)
        JsonConfigFileExtensions.cs            # mapper: on-disk -> domain (ToDomainGraph)
        ConfigFileFormat.cs                    # stateless .autocontext.json serializer (mirrors RegistryFileFormat)
        ConfigFileManager.cs                   # store/manager — port of TS AutoContextConfigManager; owns the snapshot, FS-watch (Watch/ReconcileFromWatcherAsync), and signature-based self-write suppressor; implements IConfigSnapshotAccessor + IConfigUpdater
        ConfigFileService.cs                   # hosted service — initial disk load then arms the watcher at engine start
        IConfigSnapshotAccessor.cs             # lock-free read seam (Current) that DispatchPolicy reads for Config.Get
        ConfigBatchWriter.cs                   # micro-batch write coalescer behind IConfigUpdater (P3 row 6, DONE)
        IConfigUpdater.cs                      # one-method write seam the manager satisfies (P3 row 6, DONE)
        ConfigFrameStream.cs                   # BroadcasterFrameStream<JsonConfigSnapshot, JsonConfigStreamFrame> (IBroadcasterFrameStream impl) for Config.Subscribe: drains a BroadcasterSubscription<JsonConfigSnapshot> (fanned out by ConfigFileService over a shared Infrastructure/Events/SnapshotBroadcaster<T> — snapshot-on-subscribe + per-subscriber bounded buffer, P3 row 9, DONE) and yields snapshot/dropped frames; Config.Subscribe is served by DispatchPolicy, Config.Get/ToggleFile/ToggleRule also via DispatchPolicy (the latter two via ConfigToggle + IConfigUpdater)
      Context/                                 # ~60-flag detection (Workspace.* wire surface)
        WorkspaceContextDetector.cs            # orchestrator — injected with the three rule-data lists below; runs them, emits result
        WorkspaceHandlers.cs                   # Workspace.{Detect,Info}
        # — Rule data (plain records; each file holds a `static readonly`
        #   table registered in DI as the corresponding `IReadOnlyList<T>`
        #   singleton; no interfaces — substitution is over the data, not
        #   the behaviour) —
        FileSelector.cs                        # (Value, FileSelectorKind) — one selection criterion, shared by presence + content
        FileSelectorKind.cs                    # enum: Extension | FileName | GlobPattern
        FilePresenceRule.cs                    # one row of IReadOnlyList<FilePresenceRule> — selectors → flag (presence only)
        ContentScan.cs                         # one row of IReadOnlyList<ContentScan> — manifest selectors + ContentPatternRule list (npm, .NET, …)
        ContentPatternRule.cs                  # (Flag, Regex) — body-pattern → flag, scoped under a ContentScan
        FlagActivationEdge.cs                  # one row of IReadOnlyList<FlagActivationEdge> — [child, parent] transitive activation graph
        WorkspaceDetectionRules.cs             # static partial holding the three tables (FileRules, ContentScans, FlagActivationEdges) + GeneratedRegex patterns
        # — Derived data (per-Detect outputs; plain records, not DI-registered) —
        FileExtensionsIndex.cs                 # derived ext set, fed to Discovery (P7)
    Features/                                  # outward-facing capability tier (P11): served to the extension over RPC; the engine runs without these, but without them nothing can consume anything
      Instructions/                            # runtime services
        InstructionsManifestService.cs           # merged catalog+manifest snapshot loader + reloader
        InstructionsFileBodyProjector.cs         # disabled-rule filter, [INSTxxxx] strip, override merge
        InstructionsContentIndex.cs              # in-memory content search index
        InstructionsOverrideWatcher.cs           # .github/instructions/ FS watcher (debounced); produces InstructionsOverrides snapshots
        InstructionsOverrides.cs                 # immutable snapshot of .github/instructions/ inventory (paths + basenames); consumed by InstructionsFileBodyProjector + InstructionsManifestService
        ApplyToParser.cs                         # comma + brace-expand, extension extraction (shared with the build task via `<Compile Link>`)
        InstructionsHandlers.cs                  # List/Categories/Get/GetAll/GetAlwaysAttached/GetRaw/SearchContent/Subscribe
        InstructionsFrameStream.cs               # BroadcasterFrameStream<InstructionsSnapshot, …> (IBroadcasterFrameStream impl) for Instructions.Subscribe: drains a BroadcasterSubscription<InstructionsSnapshot> (fanned out over a shared Infrastructure/Events/SnapshotBroadcaster<T> — snapshot-on-subscribe + disabled-flag re-evaluation) and yields snapshot/dropped frames
        InstructionsManifestLoader.cs            # reads Resources/instructions-catalog.json + instructions-manifest.json, merges into the snapshot
      # McpTools/ — the McpTools.{List,Invoke} capability (today's Mcp/ below) is the next tenant of this tier (P11)
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
      AgentEventFrameStream.cs                 # BroadcasterFrameStream<AgentEvent, …> (IBroadcasterFrameStream impl) for Events.Subscribe: drains a BroadcasterSubscription<AgentEvent> (fanned out over a shared Infrastructure/Events/Broadcaster<T> — pure live tail, bounded per-subscriber buffers + drop) and yields event/dropped frames
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
      EngineRpcClient.cs                       # Engine.Hello/Shutdown/RegistryEntries/WriteLog
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

  AutoContext.Instructions.Parser/        # shared parser library (net10.0) — referenced by both the generator and the engine runtime so one source is compiled for both
    AutoContext.Instructions.Parser.csproj     # TargetFramework=net10.0; class library
    InstructionsFileParser.cs                  # frontmatter reader (name/description/applyTo) + body section index + [locator#fragment] reference capture
    ApplyToParser.cs                           # applyTo splitter/brace-expander — parse only, round-trip-verified
    InstructionsFileReferenceResolver.cs       # pure cross-file resolver — validates rule/section references against an InstructionsFileCatalog (no I/O)
    # …plus the parsed-shape records (frontmatter, section index, references) and the catalog/finding types the resolver consumes

  AutoContext.Instructions.Manifest.Generator/   # build-time console generator (net10.0, AssemblyName instructions-manifest-gen) — not shipped with the engine
    AutoContext.Instructions.Manifest.Generator.csproj   # OutputType=Exe; ProjectReference → AutoContext.Instructions.Parser
    InstructionsManifestGenerator.targets      # imported by AutoContext.Engine.csproj; <Exec>s the generator during the binary's build
    Program.cs                                 # generic-host entry point (build host → resolve runner → Run → exit code)
    InstructionsManifestGenerator.cs           # runner: reads instructions-catalog.json, scans src/AutoContext.Engine/Instructions/, writes instructions-manifest.json into the binary's Resources/
    InstructionsCatalogReader.cs               # reads + cross-validates the hand-authored instructions-catalog.json (categories, label, membership, activationFlags)
    InstructionsManifestBuilder.cs             # corpus scan + per-file facts (sections, applyTo ext set, hashes) → JsonInstructionsManifest
    InstructionsManifestSerializer.cs          # deterministic, byte-stable JSON writer (no System.Text.Json dependency)
    # the engine sequences the generator via a ProjectReference (ReferenceOutputAssembly=false);
    # the target runs AfterTargets=ResolveProjectReferences BeforeTargets=CoreCompile.

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
    Resources/                                 # read-only side-cars — copied next to the binary
      instructions-catalog.json                #   hand-authored curatorial layer (tracked in source)
      instructions-manifest.json               #   build-generated per-file facts (P3)
      mcp-tools-registry.json                  #   hand-authored registry
      mcp-tools-registry.schema.json           #   JSON-schema for the registry
      mcp-tools.json                           #   build-time projection of the registry
      workers.json                             #   generated from AutoContext.Worker.* projects

  tests/
    AutoContext.Framework.Pipes.Tests/         # transport primitives — listener, codec, keep-alive, exchange/streaming triad
    AutoContext.Framework.Logging.Tests/       # EngineLoggerProvider, ingest ring, write-log client
    AutoContext.Engine.Protocol.Tests/      # DTO envelope round-trips (including LogRecord), endpoint builder, source-generated JSON contexts
    AutoContext.Framework.Workers.Tests/       # IMcpTask, WorkerHostBuilderExtensions, WorkerTaskDispatcherService, WorkerHealthMonitorService
    AutoContext.Engine.Core.Tests/             # engine-internal services + every RPC handler + lifecycle + watchdogs
    AutoContext.Client.Core.Tests/             # typed RPC clients, subscription consumers, find-or-spawn flow
    AutoContext.Engine.Tests/                  # binary-host integration: argv parser, role split, ready-marker, end-to-end spawn
    AutoContext.Instructions.Parser.Tests/     # frontmatter + applyTo parser fixtures, round-trip invariant
    AutoContext.Instructions.Manifest.Generator.Tests/  # manifest builder + serializer assertions
    AutoContext.Framework.Tests.Support/       # shared test-support reused by engine + worker tests
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
    Resources/                                 # read-only side-cars (mirror of src tree above)
      instructions-catalog.json
      instructions-manifest.json
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
| `Engine.Hello` / `Shutdown` / `RegistryEntries` / `Lifecycle.Subscribe` | `Engine.Core` | `rpc` + `events` |
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

**Status**: Completed on branch `features/framework-restructure`.

| # | Commit subject | State |
|---|---|---|
| 1 | `refactor(framework): split into pipes/logging/protocol/workers` | DONE |
| 2 | `refactor(mcp): fold IMcpTask into Framework.Workers` | DONE |
| 3 | `refactor(workers): fold WorkerHostBuilderExtensions into Framework.Workers` | DONE |
| 4 | `refactor(tests): split Framework.Tests across substrate projects` | DONE |
| 5 | `refactor(ts): rename Framework.Web to Nodejs.Core` | DONE |
| 6 | `docs(plan): correct Worker.Shared fold scope` | DONE |
| 7 | `docs(plan): mark Phase 0 complete` | DONE |

**Goal**: reshape the existing project graph into the four-project
`Framework.*` substrate the rest of the rollout consumes, fold the
two dead-weight projects (`Mcp.Abstractions`, `Worker.Shared`) into
it, and rename the shared TS substrate to its end-state identity.
This phase touches existing code only — every new engine / client /
instructions-tooling project is created in the phase that first uses it (see
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
  - `AutoContext.Engine.Protocol/` — new sub-project (no
    equivalent in today's substrate). Skeletons for the cross-side
    DTOs (protocol-version constant, endpoint builder, log-record
    envelope, discriminated-envelope base shapes, source-generated
    JSON context). Also receives `AutoContext.Framework/Workers/ServiceAddressFormatter.cs`
    — it's a pure endpoint string-formatting helper (no I/O, no
    lifetime, no DI), the same wire-shape concern `Endpoint.cs`
    owns under the engine topology; parking the legacy formatter next
    to its successor keeps both endpoint shapes in one place and
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
  - `AutoContext.Worker.Shared` (one file:
    `Hosting/WorkerHostBuilderExtensions.cs`) →
    `AutoContext.Framework.Workers/`. Delete the
    `AutoContext.Worker.Shared` project.
  - Every `Worker.*` project drops its `Mcp.Abstractions` and
    `Worker.Shared` `<ProjectReference>`s and picks up
    `<ProjectReference>`s to all four `AutoContext.Framework.*`
    projects directly.
- New test projects, one per new Framework sub-project:
  `AutoContext.Framework.Pipes.Tests`,
  `AutoContext.Framework.Logging.Tests`,
  `AutoContext.Engine.Protocol.Tests`,
  `AutoContext.Framework.Workers.Tests`.
  Today's `AutoContext.Framework.Tests` is split across the four
  substrate test projects according to which sub-project owns each
  fixture. Test projects for the *new* engine / client / instructions-tooling
  projects come up alongside those projects in their first-use
  phases.
- `AutoContext.slnx` updated for the four Framework sub-projects,
  the renamed `Nodejs.Core`, and the deletions of `Mcp.Abstractions`
  / `Worker.Shared`. No entries for engine / client / instructions-tooling
  projects yet — those are added by the phases that introduce them.
- `build.ps1` learns the new Framework project list (compile targets
  only; packaging stays out until Phase 13).

**Tests**:
- Solution builds via `.\build.ps1 Compile`.
- All existing `Worker.*` tests and the split-up Framework substrate
  tests stay green after the rename + consolidation (no behaviour
  change — the diff is purely namespace + project-graph).

**Out of scope**: every new engine / client / instructions-tooling project
(introduced in their first-use phases); any pipe binding, DI
registration, or executable host.

## Phase 1 — Engine lifecycle substrate

**Status**: Completed on branch `features/engine-lifecycle-substrate`.

| # | Commit subject | State |
|---|---|---|
| 1 | `docs(engine): rename pipe names to endpoints` | DONE |
| 2 | `feat(protocol): add Endpoint and ProtocolVersion` | DONE |
| 3 | `feat(engine-core): scaffold project with composition root and options` | DONE |
| 4 | `feat(engine): scaffold binary host with role-split argv parser` | DONE |
| 5 | `feat(engine-core): add RegistryFile{Reader,Writer,Format} single-writer owner of engine-registry.json` | DONE |
| 5b | `refactor(engine-core): make RegistryFileWriter a single-worker hosted service with named-mutex coordination and atomic temp-file writes` | DONE |
| 6 | `feat(engine-core): add LifecycleService four-pipe accept loops` | DONE |
| 7 | `feat(engine-core): add Engine.Hello handshake and protocol-version gate` | DONE |
| 8 | `feat(engine-core): add RegistryEntryBuilder and own-entry lifecycle on RegistryFileService` | DONE |
| 9 | `feat(engine): serve Engine.RegistryEntries and Engine.Shutdown over rpc` | DONE |
| 10 | `feat(engine-core): add Engine.Lifecycle.Subscribe events stream and notifier` | DONE |
| 10b | `test(engine): align lifecycle test conventions` | DONE |
| 10c | `fix(engine): bound lifecycle shutdown drain by configurable timeout` | DONE |
| 10d | `refactor(engine): unify rpc handshake and dispatch behind RpcConnectionProcessor` | DONE |
| 11 | `feat(engine-core): add idle-timeout watchdog` | **DONE** |
| 12 | `feat(engine-core): add host watchdog` | **DONE** |
| 13 | `feat(engine-core): add unique-instance guard` | **DONE** |
| 14 | `feat(engine): wire EngineCrashWriter to sinks` | **DONE** |
| 15 | `test(engine): stand up integration harness for binary spawn` | **DONE** |
| 16 | `docs(plan): mark Phase 1 complete` | **DONE** |

**Goal**: engine binds the four pipes, performs the `Engine.Hello`
handshake, manages its own idle/parent-pid/shutdown lifecycle, and
participates in the shared liveness registry.

**Design anchors**: `§ Lifecycle`, `§ Engine options (CLI surface)`,
`§ RPC surface` (`Engine.Hello`, `Engine.RegistryEntries`,
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
- `AutoContext.Engine.Protocol/` — endpoint builder (workspace
  hash + `<kind>` + `<instanceId>`; normalisation rules in `§ Endpoint`),
  protocol-version integer.
- `AutoContext.Framework.Pipes/` — used as-is. The existing
  `PipeListener` / `BoundPipeListener` pair already delivers the
  atomic-bind, multi-connection accept loop, pre-bound continuous
  listening, and drain-on-cancel semantics the engine needs; no new
  transport seams are required for the four-pipe bind.
- `AutoContext.Engine.Core/` — hosted services for: pipe accept
  loops (`rpc`, `events`, `health`, `logs` — `logs` is bound here so
  consumers see EOF cleanly, but engine record emission lives in
  Phase 2), `Engine.Hello` handler, `Engine.Lifecycle` broadcaster,
  `Engine.RegistryEntries` handler, `Engine.Shutdown` handler,
  the own-entry lifecycle folded into `RegistryFileService` (append
  on Start, best-effort remove on Stop — composing
  `RegistryEntryBuilder` for the pure construction half),
  idle-timeout watchdog, parent-pid watchdog.
- `RegistryFileReader` / `RegistryFileWriter` / `RegistryFileFormat`
  / `RegistryFileService` — sole owners of `engine-registry.json`,
  split along three orthogonal concerns so `§ P9`'s
  single-writer-per-resource rule is enforced by *type identity*
  and `§ P8`'s end-to-end async stays honest at the public API
  even though the OS `Mutex` primitive demands thread affinity:
  (a) `RegistryFileFormat` owns the envelope shape, schema-version
  contract, and `JsonSerializerOptions` (stateless, shared by
  reader and writer); (b) `RegistryFileReader` is the passive
  concurrent-read surface — opens with
  `FileShare.ReadWrite | FileShare.Delete` so it coexists with the
  writer's atomic rename, retries under exponential backoff, and
  tolerates corrupt/unknown-schema files by returning an empty
  list; (c) `RegistryFileWriter` is an `internal` atomic
  single-shot writer — temp+fsync+rename only, no mutex, no retry,
  no RMW — small enough that its only correctness obligation is
  that the real file is replaced atomically; (d) `RegistryFileService`
  is the public hosted coordinator that owns *all* complexity:
  a single dedicated background thread runs a fully synchronous
  worker loop (so the named cross-process `Mutex`'s acquire/release
  affinity is satisfied by construction), a `Channel<WriteRequest>`
  serialises in-process callers, a session-local
  `AutoContext.RegistryFile.{sha256(path)[..16]}` named mutex
  serialises cross-process peers (the registry lives under the
  per-user cache root, so session-local scope matches the
  contention surface; a `Global\` prefix would require
  `SeCreateGlobalPrivilege` and falsely couple unrelated users),
  and a per-request
  `TaskCompletionSource` keeps the `WriteAsync` API honestly async.
  The service handles `AbandonedMutexException` gracefully
  (atomic-rename writer guarantees no torn intermediate state, so
  reclaiming the mutex is always safe). Every consumer (this phase's
  own-entry lifecycle on `RegistryFileService`, Phase 2b's
  `RegistryEntryReader`, any future peer-watcher) composes over
  the appropriate surface: writers go through
  `RegistryFileService`, readers go through `RegistryFileReader`. Born in Phase 1 because Phase 1 is when
  `engine-registry.json` is first written; Phase 2b composes over
  `RegistryFileReader` rather than reaching into the file directly.
- `engine-registry.json` entry lifecycle per
  `§ Housekeeping` and the `engine-registry.json entry lifecycle`
  pitfall: append-on-start (fresh `instanceId` every spawn; no
  upsert), remove-on-graceful-shutdown, leave-stale-on-crash. The
  locking, `FileShare.None` writer window, atomic temp+rename, and
  exponential-backoff reader retry are owned by the reader/writer
  pair above; this bullet pins the *lifecycle* of the entry, not
  the file mechanics.
- The same-`instanceId`-collision rule (`§ Lifecycle` *Concurrent
  first-connect*) — a second engine binding under the same
  `--instance-id` is a launcher bug under the per-launch-UUID
  contract (P4); the engine fails loudly on pipe-bind collision
  with a non-zero exit. `PerWorkspaceInstanceGuard` (the sole
  production `IUniqueInstanceGuard` impl, called at the top of
  `LifecycleService.StartAsync` before any pipe bind) is the
  fail-fast sanity check enforcing this contract; it probes the
  would-be `rpc` endpoint and throws `IOException` when a peer
  already holds the address. The design does **not** treat the
  collision as a shape bind has to be idempotent against.

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
- `EngineCrashWriter` produces a parseable `crash.log` under
  `…\<workspaceHash>\<instanceId>\logs\` when an unhandled
  exception escapes `DaemonHostFactory.RunAsync`, when a non-main
  thread raises via `AppDomain.UnhandledException`, and when an
  unobserved `Task` faults; graceful `Engine.Shutdown`,
  idle-timeout, and parent-pid watchdog exits produce **no**
  `crash.log`. A deliberately broken write target (read-only
  directory) does not mask the original fault — the process still
  exits with the original non-zero code.
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
the `EngineCrashWriter` it depends on is wired up here. Worker spawn
(Phase 7).

## Phase 2 — Engine logging pipeline and cache housekeeping

**Status**: Completed on branch `features/engine-logging-and-housekeeping`.

| # | Commit subject | State |
|---|---|---|
| 1 | `feat(protocol): introduce LogRecord wire envelope` | DONE |
| 2 | `feat(engine-core): add log ingest channel and engine.log file writer` | DONE |
| 3 | `feat(engine-core): route engine ILogger<T> through ingest channel via EngineLoggerProvider` | DONE |
| 4 | `feat(engine-core): add RetentionPolicy and log rotation with RotatedLogCleaner` | DONE |
| 5 | `feat(engine-core): fan out engine records on logs pipe with per-subscriber buffer and slow-subscriber eviction` | DONE |
| 6 | `feat(engine): serve Logs.GetEngine over rpc` | DONE |
| 7 | `feat(engine-core): add RegistryEntryReader` | DONE |
| 8 | `feat(engine-core): add SubtreeRegistryStatus and CacheRootScanner` | DONE |
| 9 | `feat(engine-core): add StaleSubtreeCleaner and HousekeepingService shutdown sweep` | DONE |
| 10 | `test(engine): integration test for cross-engine shutdown-sweep cleanup` | DONE |
| 11 | `docs(plan): mark Phase 2 complete` | DONE |

Two equal-tier features land together because they share the
per-instance subtree shape (both write under it) and the
`engine-registry.json` reader (`RegistryEntryReader` consults the
same entries `RegistryFileService` produces). Neither is subordinate to
the other; each gets its own subsection below. Rows 1–6 implement
2a (engine logging pipeline); rows 7–10 implement 2b (cache
housekeeping). Row 4's `RetentionPolicy` is the shared seam both
features compose over, so it must land before row 9.

### 2a — Engine logging pipeline

**Goal**: every record the engine emits via `ILogger<T>` lands in
`engine.log` under the per-instance subtree, fans out on the `logs`
pipe and `Logs.TailEngine` RPC subscribers, rotates per `--logging`,
and rotated files are cleaned per `--retention`.

**Design anchors**: `§ Log categories`,
`§ RPC surface` (`Logs.GetEngine`, `Logs.TailEngine`,
`Engine.WriteLog` envelope shape), `§ P9` (slow-subscriber drop),
`§ Log pipeline backpressure` pitfall.

**Code touch**:
- `AutoContext.Engine.Protocol/LogRecord.cs` — the canonical wire
  envelope (`timestamp`, `category`, `level`, `eventId?`, `message`,
  `properties?`, `exception?`). Phase 2a collapses today's substrate
  pair `LogEntry`/`JsonLogEntry` into this single record under
  Protocol's ownership; `Framework.Logging` keeps the worker-side
  logger provider and the legacy sideband sink, but the envelope
  itself moves to where every other cross-side DTO lives (P1: one
  record envelope; P3: wire shape owned by Protocol).
- `AutoContext.Engine.Core/Logging/` — engine-side log sink:
  one ingest channel (`LogChannel`), one drain loop
  (`LogFileSinkService`) that dispatches each drained record to
  inner sinks — the file sink (today only; routes to
  `engine.log` / `worker-<id>.log` from row 8 by `category`
  prefix) and, from row 5, the `logs`-pipe / `Logs.Tail*`
  broadcaster (per-subscriber bounded buffer; slow subscribers
  dropped with a terminal
  `{ kind: "dropped", reason: "slow-subscriber" }` frame). Row 5
  reshapes `LogFileSinkService` from "drain-and-write" into
  "drain-and-dispatch" so the broadcaster sits next to the file
  sink as a sibling inner sink rather than as a second consumer
  of `LogChannel` (the channel stays single-reader).
- Rotation per `--logging` thresholds (1k lines / 5 MB normal; 5k /
  25 MB debug); rotated-file naming `engine-<iso8601>.log`.
- `RotatedLogCleaner` deletes rotated files older than the
  `--retention` window during the next rotation. (Per-tenant
  cleanup inside a *live* subtree; whole-subtree cleanup is
  Housekeeping's job — see 2b. The two share `RetentionPolicy`
  as their single reader of the `--retention` option.)
- `Logs.GetEngine` handler (active file only; `opts.lastN`,
  `opts.since`, `truncated` flag). `crash.log` is intentionally
  **out of scope** for the `Logs.*` RPC surface: it is a write-once
  tombstone produced by Phase 1's `EngineCrashWriter`, not a
  tail-able feed, and is reaped along with the rest of the
  per-instance subtree by 2b housekeeping under `--retention`.
- `Logs.TailEngine` is **deferred to the Phase 3 prelude** — it
  needs a server-streaming-response convention on the `rpc` pipe
  (today the dispatcher is strictly one-request → one-response),
  and that convention is shared infrastructure with
  `Config.Subscribe` / `Instructions.Subscribe` in Phase 3. Building
  it once, on the cusp of Phase 3, avoids landing unused streaming
  plumbing in Phase 2. Interim consumers tail the `logs` pipe
  directly (raw NDJSON, no handshake) per the design's documented
  fallback.

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
- Slow-subscriber drop (against the row-5 `logs`-pipe / future
  `Logs.Tail*` broadcaster): a subscriber that doesn't drain gets
  the terminal `dropped` frame and is disconnected; other
  subscribers and the file sink keep progressing.

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
- `AutoContext.Engine.Core/Machine/Housekeeping/HousekeepingService` —
  hosted service that runs the **shutdown sweep only**. No startup
  sweep: under the per-launch-UUID contract (P4) every engine's
  `<instanceId>` is fresh on every spawn, so the registry stays
  append-only and there is nothing to reconcile before pipe-bind.
  Cleanup of any peer's leftover subtree happens at this engine's
  own graceful shutdown, after `RegistryFileService` has removed
  this engine's own entry (via its own `StopAsync` prefix) and
  `LifecycleService` has closed the four pipes. Hosted-service
  registration order pins the invariant: register
  `HousekeepingService` **after** `RegistryFileService` (and
  before `LifecycleService`) so its `StopAsync` runs *before*
  the file service's — reverse-registration order — letting
  the sweep traverse the still-live `RegistryFileService.WriteAsync`
  channel before it's torn down, while still observing the
  on-disk registry in its post-pipe-close shape. Bounded
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
  lookup against `RegistryFileReader` (Lifecycle/, Phase 1). The
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
- `RegistryEntryReader` — composes over `RegistryFileReader`
  (Lifecycle/, Phase 1) to read all entries, applies the
  `Process.StartTime` peer-liveness check, and supplies the
  registration half of `CacheRootScanner`'s classification. The file
  mechanics (retry, corrupt-file tolerance) live in
  `RegistryFileReader` — this entry reader only adds the liveness
  check on top of the entry data it gets back.
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
- Shutdown sweep runs after `RegistryFileService` has removed this
  engine's own entry (during its own `StopAsync`) and after
  `LifecycleService` has closed the four pipes — a peer that
  starts mid-shutdown does not observe this engine's entry as live.
- Integration: spawn two engines against the same cache root,
  hard-kill one (skipping its shutdown sweep), then gracefully
  shut down the survivor; assert the survivor reaps the killed
  engine's subtree as part of its own shutdown sweep, and that no
  live subtree was touched.

**Out of scope** (2a): worker records (Phase 8); `Logs.GetWorker`
/ `Logs.TailWorker` (Phase 8); `Logs.TailEngine` (deferred to the
Phase 3 prelude — see row 6 code touch above). 2b has no
out-of-scope carve-out;
its dependency on Phase 1's `RegistryFileReader` /
  `RegistryFileWriter` and `RegistryFileService` (which owns both
  the file mechanics and this engine's own-entry lifecycle, and
  supplies the on-disk `engine-registry.json` entries the reader
  surfaces) is declared under code touch, not deferred work.

## Phase 3 — Config store

**Status**: Completed on branch `features/config-store`.

| # | Commit subject | State |
|---|---|---|
| 1 | `feat(engine-core): support rpc server-streaming` (prelude) | DONE |
| 2 | `feat(engine): serve Logs.TailEngine over rpc` (prelude) | DONE |
| 3 | `feat(engine-core): port AutoContextConfigManager to AutoContextConfig` | DONE |
| 4 | `feat(engine-core): add FileChangeWatcher with trailing-edge debounce reload` | DONE |
| 5 | `feat(engine-core): add deep-equal self-write suppressor` | DONE |
| 6 | `feat(engine-core): add writer mutex and micro-batch write coalescing` | DONE |
| 7 | `feat(engine): serve Config.Get over rpc` | DONE |
| 8 | `feat(engine): serve Config.ToggleFile and Config.ToggleRule over rpc` | DONE |
| 9 | `feat(engine-core): add Config.Subscribe events stream with snapshot-on-subscribe` | DONE |
| 10 | `test(engine): integration test for cross-instance config reload coalescing` | DONE |
| 11 | `docs(plan): mark Phase 3 complete` | DONE |

**Landed-row notes.**

- **Row 4 — `FileChangeWatcher` with trailing-edge debounce.**
  Extracted a reusable `TrailingEdgeDebouncer` (capacity-one channel
  + `TimeProvider`-scheduled quiet window) into `Infrastructure/Events`,
  wrapped by a `FileChangeWatcher` in `Infrastructure/IO` that forwards
  `FileSystemWatcher` events as signals; the manager's
  `ReconcileFromWatcherAsync` → `RefreshAsync` runs once per settled
  burst. Manager ctor gains `timeProvider` / `debounceDelay`
  (default 100&#160;ms). Deterministic `FakeTimeProvider` tests via
  `Microsoft.Extensions.TimeProvider.Testing`.
- **Row 5 — deep-equal self-write suppressor.** Landed in row 3,
  signature-based: SHA-256 of the on-disk bytes compared against
  `_lastSignature`.
- **Row 6 — writer mutex and micro-batch write coalescing.** Added a
  `ConfigBatchWriter` that the manager owns and exposes through an
  additive `UpdateBatchAsync` API, leaving the synchronous
  `UpdateAsync` primitive untouched. An unbounded single-reader
  channel drains queued edits, a `TimeProvider`-scheduled micro-batch
  window (default 5&#160;ms) folds every edit that lands inside it
  into one write call (one write, one snapshot swap, one fan-out),
  and each caller's task completes when its batch is applied. The
  writer depends on a one-method `IConfigUpdater` seam the manager
  satisfies directly (its existing `UpdateAsync` matches), keeping the
  two decoupled and unit-testable against a fake. Per-edit
  cancellation drops the edit from its batch; `Dispose` cancels
  in-flight edits. Deterministic `FakeTimeProvider` tests. The
  revision counter and `{ revision, changes }` envelope stay deferred
  to rows 8-9.
- **Row 7 — serve `Config.Get` over rpc.** The snapshot reaches
  clients without a streaming subscription: `ConfigFileManager`
  exposes its lock-free `Current` snapshot through an
  `IConfigSnapshotAccessor` seam, and `ConfigFileService` (a hosted
  service registered before `LifecycleService`) performs the initial
  disk load and arms the watcher at engine start so the snapshot is
  populated before the first request can land. `DispatchPolicy` routes
  `Config.Get` to a unary handler that projects the current snapshot
  onto the `Config.*` wire DTOs via
  `ConfigSnapshotExtensions.ToWireFormat`. The revision counter and
  the `Config.Subscribe` fan-out stay deferred to rows 8-9.
- **Row 8 — serve `Config.ToggleFile` / `Config.ToggleRule` over
  rpc.** Both methods flip state (matching the `Toggle` name): a pure
  `ConfigToggle` transform takes the current `ConfigSnapshot` and
  returns a new one with the targeted instruction file's whole-file
  disabled flag (`ToggleFile`) or a single rule's disabled flag
  (`ToggleRule`) flipped — creating the entry when absent and pruning
  it when the edit leaves it carrying no state, so the in-memory graph
  matches a reload. `DispatchPolicy` routes both methods to unary
  handlers that validate params (rejecting missing `name`/`ruleId`
  with `InvalidParams`), hand the transform to the manager through the
  existing one-method `IConfigUpdater` seam, and reply with the
  refreshed snapshot via `ToWireFormat`. The `{ revision, changes }`
  envelope and the `Config.Subscribe` fan-out stay deferred to row 9.
- **Row 9 — `Config.Subscribe` events stream with
  snapshot-on-subscribe.** Additive on top of the row 1-2 prelude,
  mirroring `Logs.TailEngine`'s broadcaster. A singleton
  `SnapshotBroadcaster<JsonConfigSnapshot>` (the shared
  `Infrastructure/Events/` snapshot-on-subscribe core) caches the latest
  `JsonConfigSnapshot` and replays it as the first frame to every new
  subscriber (snapshot-on-subscribe — keyed state, not a pure live
  tail, so a late subscriber never needs a separate `Config.Get`).
  Each subscriber owns a bounded `Channel` (capacity 64); a
  sustainedly slow subscriber is dropped with a terminal
  `JsonConfigDroppedFrame` (reason `slow-subscriber`) while the
  remaining subscribers keep flowing, and graceful `Complete` closes
  every channel with a clean EOF (no terminal frame). Frames travel
  as a discriminated `JsonConfigStreamFrame` envelope
  (`kind: "snapshot" | "dropped"`). `ConfigFileService` bridges the
  manager's change event into `TryPublish` and primes the cache once
  at startup with the disk-loaded snapshot (the initial load raises no
  change event). `DispatchPolicy` routes `Config.Subscribe` to a
  streaming handler whose subscription disposal is handed off to
  `StreamingHandlerResult.PostFlush`, so the processor's `finally`
  releases the broadcaster slot on peer-close, cancellation, or
  iterator fault. The `{ revision, changes }` envelope and
  batch-coalescing fan-out stay deferred.

**Prelude — server-streaming responses on the `rpc` pipe.** Phase 3
is the first phase that needs `*.Subscribe` semantics
(`Config.Subscribe`), and Phase 2 row 6 deferred `Logs.TailEngine`
for the same reason. Before Phase 3's first row lands, two
additional commits ship on a Phase 3 prelude branch:

1. `feat(engine-core): support rpc server-streaming` — **DONE**.
   Introduces the discriminated `JsonRpcStreamFrame` envelope
   (`kind: "next" | "complete" | "error"`, request id echoed on
   every frame as a future-multiplex correlator) and splits
   `RpcHandlerResult` into two sealed records: `UnaryHandlerResult`
   (the existing single-`JsonRpcResponse` shape) and
   `StreamingHandlerResult` (an `IAsyncEnumerable<JsonElement>` of
   payloads, terminated by exactly one synthesised
   `JsonRpcStreamComplete` or `JsonRpcStreamError` frame).
   `RpcConnectionProcessor` type-switches on the result; streaming
   is always terminal (one stream per connection); cancellation /
   peer-close exit without a terminal frame; iterator faults
   surface as a structured `JsonRpcStreamError` (generic message
   on the wire, full exception logged). `PostFlush` runs in a
   `finally` for streaming so handler-supplied cleanup
   (subscription disposal) cannot leak even when the peer hangs
   up mid-stream.
2. `feat(engine): serve Logs.TailEngine over rpc` — **DONE**.
   The deferred half of Phase 2 row 6, now trivial on top of (1):
   `DispatchPolicy` consumes the logs
   `Broadcaster<JsonLogRecord>.Subscribe()` and maps it through
   `LogFrameStream` to yield each
   `JsonLogStreamFrame` (record/dropped) as a `JsonElement`;
   subscription disposal is handed off to
   `StreamingHandlerResult.PostFlush` so the processor's `finally`
   guarantees the broadcaster slot is released even on
   peer-close, cancellation, or iterator fault.

With those landed, `Config.Subscribe` and every later `*.Subscribe`
row becomes a small additive change.

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
- `AutoContext.Engine.Core/Workspace/Config/ConfigFileManager` —
  port of today's `AutoContextConfigManager` (TS) into .NET. JSON
  shape unchanged; `.autocontext.json` keys are camelCase only
  (`mcpTools`, `disabledTasks`, `enabled`), matching the existing
  TS model — no dual-casing, no key normalisation. The manager owns
  the live snapshot and exposes `LoadAsync` / `RefreshAsync` /
  `UpdateAsync` / `Watch` with a `Changed` event. It implements
  `IConfigSnapshotAccessor` (the lock-free `Current` read seam) and
  `IConfigUpdater` (the write seam); a `ConfigFileService` hosted
  service performs the initial disk load and arms the watcher at
  engine start so the snapshot is populated before the first
  `Config.Get` can land.
- Three-participant config model split out from the manager: an
  immutable **domain graph** (`ConfigSnapshot` + `ConfigDiagnostic` +
  `ConfigInstructionsFile` + `ConfigMcpTool`, pure data, no
  behaviour) that the rest of the engine reads, an **on-disk wire
  DTO** layer (`JsonConfigFile` + `JsonConfigFile*` records, plus
  `JsonConfigFileMcpToolValueConverter` for the `false | object`
  `mcpTools` union) that mirrors the file shape byte-for-byte, and
  the **`Config.*` RPC wire DTOs** (`JsonConfig*` under
  `Engine.Protocol/Messages/Config`) the engine streams to clients.
  `ConfigSnapshotExtensions.ToFileFormat` (domain -> on-disk) and
  `JsonConfigFileExtensions.ToDomainGraph` (on-disk -> domain) are the
  crossing points for persistence, while
  `ConfigSnapshotExtensions.ToWireFormat` (domain -> RPC) projects the
  snapshot onto the transport; the domain graph never leaks either
  wire shape. `ConfigFileFormat` owns the JSON options, key order,
  and parse normalisation.
- `FileSystemWatcher` + per-resource trailing-edge debounce
  (~75–150 ms, `EngineOptions` constant). Reads on timer fire only,
  never inside the watcher callback. Cancellation propagates through
  the engine's root token (P8). **Status:** the watcher itself landed
  in row 3 (`Watch` starts it; `ReconcileFromWatcherAsync` calls
  `RefreshAsync`), but it fires `RefreshAsync` directly on every FS
  event — the trailing-edge debounce (and its `EngineOptions`
  constant) is still row 4. Until it lands, an atomic-rename or
  truncating-save burst triggers one read per raw FS event; the
  signature suppressor keeps each redundant read from fanning out,
  but the reads themselves are not yet coalesced.
- Deep-equal short-circuit (self-write suppressor): post-debounce
  parse compared by content hash against the current snapshot's
  source hash; equality skips the swap, the fan-out, and the
  revision bump. **Status:** landed in row 3 as a signature
  comparison — `PersistAsync` records the SHA-256 of the bytes it
  wrote in `_lastSignature`, and `RefreshAsync` skips the swap and
  fan-out when the freshly-read file hashes to the same value. This
  is the self-write suppressor; the only piece still owed is the
  revision-bump skip, which arrives with the revision counter (rows
  8–9).
- Writer mutex (`SemaphoreSlim`, P9). Writer-side micro-batch window
  (~5–10 ms) folds queued `Config.Toggle*` calls into one
  on-disk write, one snapshot swap, one fan-out envelope of shape
  `{ revision, changes: [...] }`. **Status:** landed in row 6 — the
  `_gate` semaphore serialises every `UpdateAsync` against watcher
  reconciliations, and `ConfigBatchWriter` (reached through
  `UpdateBatchAsync`) folds a burst of enqueued edits into a single
  `UpdateAsync` (one write, one swap, one `Changed` fan-out). The
  `{ revision, changes }` envelope shape still waits on the revision
  counter (rows 8–9).
- `Config.Get`, `Config.Subscribe`, `Config.ToggleFile`,
  `Config.ToggleRule` handlers. **Status:** `Config.Get` landed in
  row 7 (`DispatchPolicy.HandleConfigGet` projects the accessor's
  current snapshot through `ToWireFormat`); the `Config.Toggle*`
  handlers landed in row 8 (`DispatchPolicy` routes both to unary
  handlers that hand a pure `ConfigToggle` transform to the manager
  via `IConfigUpdater` and reply with the refreshed snapshot);
  `Config.Subscribe` is still row 9.
- Snapshot-on-subscribe (P6) — every new subscriber receives the
  current state as the first frame.

**Cohesion with the row-3 manager (open decisions for rows 8–10).**
The toggle round-trip is: a client (extension tree view, hook)
calls `Config.ToggleFile` / `Config.ToggleRule` on the `rpc` pipe →
the handler calls `ConfigFileManager.UpdateAsync(edit)` with
a pure `with`-expression that flips the `Disabled` flag on the
matching `ConfigInstructionsFile` / `InstructionsRule` /
`ConfigMcpTool` / `McpTask` → `UpdateAsync` persists (recording the
write signature), swaps the snapshot, and raises `Changed` → the
`SnapshotBroadcaster<JsonConfigSnapshot>` fans the new snapshot out to
`Config.Subscribe` subscribers. The local writer's own subsequent
FS-watcher event is then suppressed by the signature match, so the
toggle fans out exactly once; peer engines pick up the disk write
through their own watcher → `RefreshAsync` → `Changed`. The
immutable domain graph makes the `edit` delegate trivial, and the
gate already serialises edits against watcher reconciliations, so
the *single-engine* flow is sound today. The writer-side batch
coalescing seam landed in row 6 (`ConfigBatchWriter`); four seams
still have to be resolved as rows 8–10 land, and the manager's
current shape constrains how:
  - **Revision counter.** `Changed` is `EventHandler<ConfigSnapshot>`
    and carries no revision. The fan-out envelope is
    `{ revision, changes: [...] }`, so the revision must be assigned
    when the snapshot is published. Cleanest is to mint it inside
    `UpdateAsync` / `RefreshAsync` under the gate (monotonic `long`,
    per-instance) and widen `Changed` to carry `(snapshot,
    revision)` rather than have the broadcaster invent one outside
    the lock and risk reordering.
  - **`changes` delta.** The broadcaster needs the *previous*
    snapshot to compute the `changes` list, but `Changed` exposes
    only the new one. Either widen the event to `(previous, next,
    revision)` or have the toggle handlers emit their own change
    descriptors; decide before row 8/9 so the delta is computed
    under the gate, not reconstructed afterwards.
  - **No-op detection granularity.** `UpdateAsync` skips the write
    only on `ReferenceEquals(edited, current)`, i.e. the handler
    must return the *same* instance for a true no-op. A toggle that
    rebuilds an equal-but-not-same graph (e.g. setting a flag to the
    value it already holds) would still write and fan out. Since the
    domain records have value equality, switching the guard to `==`
    would absorb these redundant toggles cheaply — worth doing when
    the toggle handlers land.
  - **`FileShare` mode.** `PersistAsync` writes with
    `FileShare.Read`; *Process scoping* mandates `FileShare.None` +
    exponential-backoff retry so two engines can't interleave a
    read-modify-write. The single-engine path is unaffected, but the
    write-share mode and retry must tighten when cross-instance
    coordination (row 10) is exercised.

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

**Status**: Completed on branch `features/workspace-detection`.

| # | Commit subject | State |
|---|---|---|
| 1 | `feat(protocol): add Workspace.Detect and Info wire DTOs` | DONE |
| 2 | `feat(engine-core): add workspace detection rule tables` | DONE |
| 3 | `refactor(engine-core): model rules with file selectors and content scans` | DONE |
| 4 | `feat(engine-core): add WorkspaceContextDetector` | DONE |
| 4b | `refactor(engine-core): walk workspace via FileSystemEnumerator` | DONE |
| 4c | `refactor(engine-core): extract file classifier and contribution index` | DONE |
| 5 | `feat(engine-core): derive extensions index from file rules` | DONE |
| 6 | `feat(engine): serve Workspace.Detect over rpc` | DONE |
| 7 | `feat(engine): serve Workspace.Info over rpc` | DONE |
| 8 | `test(engine): cover per-flag detection and activation cascade` | DONE |
| 9 | `test(engine-core): smoke incremental watch detection` | DONE |
| 9b | `test(engine): cover Workspace.Detect and Info over rpc end-to-end` | DONE |
| 10 | `docs(plan): mark Phase 4 complete` | DONE |

**Goal**: engine runs `Workspace.Detect` on startup against its
own `--workspace` path, exposes the result via `Workspace.Detect` and
`Workspace.Info`, and produces the `extensions[]` index the coarse
`applyTo` filter consumes in Phase 6.

**Design anchors**: `§ RPC surface` (`Workspace.*`),
`§ P7` (coarse/fine match split), the ~60-flag table in
`§ RPC surface` *`Detect` return shape*.

**Code touch**:
- **Rule tables (rows 2–3).** The detection rules land as three
  `static readonly` lists of plain records — `FilePresenceRule`,
  `ContentScan`, `FlagActivationEdge` — registered in DI as the
  corresponding `IReadOnlyList<T>` singletons. No `I*Rules`
  interfaces, no provider types: the detection probes are different
  *operations* (filesystem traversal for presence, manifest-body
  regex for content scans) and the activation graph is a third
  concept entirely (graph closure, no FS, no file content) —
  collapsing them under one `I*Rules` interface would be
  shape-driven naming, not concept-driven naming. Per-flag test
  fixtures compose the detector with trimmed lists; the substitution
  surface is *data*, not *behaviour*, which is what `IReadOnlyList<T>`
  already gives us — the "no interface without a second impl"
  invariant therefore never fires here. Same flag names, same regex
  patterns, same `[child, parent]` activation edges as the TS port;
  no rule expansion, the existing ~60-flag set is the contract.
- **Content detection is grouped by manifest, not split by platform.**
  The TS port carries two parallel `npmContentRules` /
  `dotnetContentRules` arrays whose elements are byte-for-byte the
  same shape (`{ flag, pattern }`), differing only in which manifest
  the patterns scan. Modelling that as two identical record types
  (`NpmContentRule`, `DotNetContentRule`) would mean a third platform
  is a third identical type. Instead a single `ContentScan` groups a
  set of file selectors with the `ContentPatternRule` probes run
  against those files' bodies, so a new platform is a *data* edit —
  one more `ContentScan` row — not a new type:

  ```csharp
  internal sealed record FileSelector(string Value, FileSelectorKind Kind);

  internal sealed record ContentScan(
      IReadOnlyList<FileSelector> Selectors,
      IReadOnlyList<ContentPatternRule> Rules);

  internal sealed record ContentPatternRule(string Flag, Regex Pattern);

  internal enum FileSelectorKind { Extension, FileName, GlobPattern }
  ```

  The `ContentScans` table holds two entries: the npm scan
  (`package.json` by `FileName`, 12 patterns, case-sensitive except
  `hasGraphql`) and the .NET scan (`csproj`/`fsproj`/`vbproj` by
  `Extension`, 20 patterns, all case-insensitive). Case sensitivity
  lives inside each `Regex`, never at the type level. Grouping the
  selectors with their rules mirrors the detector's
  read-each-manifest-once loop, so the table shape already matches
  how the scan executes. DI collapses from four singletons to three.
- **File rules are *not* a 1:1 port of the TS glob strings.** Row 2
  shipped `FilePresenceRule(Flag, IReadOnlyList<string> Globs)` as
  a faithful port; row 3 reshapes it into a typed-selector model so
  the detector never has to re-parse glob *strings* the way the TS
  reverse maps do (`/^\*\*\/\*\.\{([^}]+)\}$/`):

  ```csharp
  internal sealed record FilePresenceRule(
      string Flag,
      IReadOnlyList<FileSelector> Selectors);
  ```

  A rule matches if **any** selector matches (pure OR); each selector
  is one criterion. `FileSelector` is a named record (not a bare
  tuple) because it is reused at two call sites — `FilePresenceRule`
  *and* `ContentScan` — and `FileSelectorKind` names the shared
  selection vocabulary; the `(Value, Kind)` pair is deliberate, with
  no nullable-triple invariant and no speculative modifier
  properties. `FilePresenceRule` keeps the "presence only — never
  reads the body" precision in its name (the body scan is
  `ContentScan`'s job). Of the 32 file rules, most are `Extension`
  selectors, nine add a `FileName` selector (`Cargo.toml`,
  `go.mod`, `pom.xml`, …), and exactly two need a real
  `GlobPattern` (`**/Dockerfile*`, the Unity
  `ProjectSettings.asset` path). The selector set is the single
  source of truth from which the detector derives both its
  classification dictionaries and its watch filter.
- **`WorkspaceContextDetector` (row 4) — single-pass, inverted
  index.** The TS detector issues ~40 separate `vscode.findFiles`
  globs plus a parallel hand-maintained watch-glob list; the engine
  has no indexed `findFiles`, so a verbatim port would mean ~40
  tree walks. Instead, an index built once from the selectors —
  `byExtension`, `byFileName`, and the small glob list — lets a
  **single** workspace traversal classify every file by lookup.
  Incremental updates use an inverted index rather than the TS
  re-glob-on-delete scheme:
  - `_contributions`: path → the set of base flags that file
    currently contributes.
  - `_baseCounts`: base flag → live contributor count; a base flag
    is on iff its count `> 0`.

  Each watcher event reclassifies **one** path (no FS walk, no
  glob), diffs its old vs new contribution set, adjusts the counts,
  and re-runs only the cheap activation cascade. Deletes are O(1)
  — "is there another file satisfying this flag?" is a count check,
  not a re-glob — and a content change rereads exactly the one
  changed manifest. Cold start seeds the same index in the single
  opening pass, so one mechanism serves both cold start and warm
  updates. The recursive watch is a raw `FileSystemWatcher`
  (`IncludeSubdirectories = true`) whose in-code filter is derived
  from the selector dictionaries, with `node_modules` / `bin` /
  `obj` / `.git` excluded. **This derivation closes a latent TS
  drift bug**: `hasYaml`, `hasDart`, `hasRuby`, and `hasSwift` are
  in `fileRules` but their extensions are absent from
  `existenceWatchGlob`, so today they are detected on a full scan
  yet never update incrementally. Deriving the watch set from the
  rules makes that class of drift unrepresentable. Synthetic flags
  (`hasGit`, `hasNodeJs`) and the cascade-to-fixpoint semantics are
  preserved exactly.
- **Workspace walk via `FileSystemEnumerator` (row 4b).** Row 4
  shipped the single opening pass as a hand-rolled `Stack<string>`
  walk that opened each directory twice — once via
  `Directory.EnumerateDirectories` to find subdirectories to prune
  and recurse, once via `Directory.EnumerateFiles` to yield files —
  with a `SafeEnumerate` wrapper that drove the enumerator manually
  so a mid-walk `IOException` could be swallowed (the BCL's lazy
  `Enumerate*` can't `yield` inside a `try`/`catch`, and an
  `EnumerationOptions { RecurseSubdirectories = true }` walk can't
  prune a subtree). Row 4b replaces that with a single
  `FileSystemEnumerator<string>` subclass: one directory open per
  directory (files and subdirectories reported from the same native
  scan), `ShouldRecurseIntoEntry` prunes `node_modules` / `bin` /
  `obj` / `.git` against the zero-allocation
  `ReadOnlySpan<char>` `FileSystemEntry.FileName` (no per-entry path
  string, no `Path.GetFileName`), and `ContinueOnError` skips the
  single offending entry rather than aborting the rest of a
  directory the way `SafeEnumerate`'s `yield break` did — the
  resilience that matters for a detector pointed at a workspace
  being actively edited. The lambda-driven `FileSystemEnumerable<T>`
  was considered and rejected for this row precisely because it has
  no `ContinueOnError` seam: it relies on `IgnoreInaccessible` for
  the common case and propagates any other mid-walk fault, which is
  weaker than the per-entry skip the subclass gives. `IsExcluded`
  (the watcher-event relative-path filter) is a separate code path
  and is unaffected.
- **Derived `extensions[]` (row 5).** A plain record produced by
  one `Detect` call — owned by the result, not DI-registered (no
  shared lifetime to manage). Built from the union of the
  `Extension`-kind selectors of every active file-rule flag, so a
  new file-rule flag automatically extends the extension set;
  content-scan flags contribute none.
- **`Workspace.Detect` and `Workspace.Info` handlers (rows 6–7).**
  The detector has **no** business with `.github/instructions/`
  content — that inventory is owned by
  `Instructions/InstructionsOverrideWatcher` (Phase 6) and
  reachable via `Instructions.List`. The TS reference port
  (`src/AutoContext.VsCode/src/workspace-context-detector.ts`)
  already enforces this split: it does not scan overrides;
  `instructions-files-override-watcher.ts` does. The .NET port
  mirrors that separation of concerns.

**Tests**:
- One fixture-per-flag test asserting each rule fires only on its
  declared trigger.
- Activation cascade: `hasNextJs` triggers `hasReact` triggers
  `hasNodeJs` without re-running the file scans.
- Incremental updates via the inverted index: creating a file flips
  its flag on; deleting the *last* contributor flips it off while a
  surviving sibling keeps it on (count-based, no re-glob); a
  manifest content change reclassifies only that file.
- Watch-filter derivation regression: every file-rule extension is
  present in the derived watch set (guards against the TS
  yaml/dart/ruby/swift drift).
- `extensions[]` derivation matches the union of every active
  file-rule flag's `Extension` selectors; content-scan flags
  contribute none.
- `Workspace.Info` returns engine-process metadata distinct from
  `Detect`.
- `Workspace.Detect` return shape carries **no** `overrides` field
  (negative-shape test against the wire contract): a workspace with
  files under `.github/instructions/` produces the same `Detect`
  envelope as a workspace without — the detector is blind to
  override content.
- End-to-end over rpc (row 9b, integration suite): spawning the
  `autocontext-engine` binary against a populated `--workspace` and
  calling `Workspace.Detect` / `Workspace.Info` over the `rpc` pipe
  returns the startup-scan flag set, the derived `extensions[]`, the
  engine version, and the spawned instance identity — the same
  contract the in-process handler tests assert, now exercised across
  a real process boundary and serialised wire frames (including the
  negative `overrides`-shape check). Gated `Category=Smoke`.

**Out of scope**: `Discovery.RouteForPrompt` extension index (Phase 9
— consumes the same data but lives in its own service).

## Phase 5 — Instructions corpus build-time pipeline

**Status**: Completed on branch `features/instructions-corpus-build-time-pipeline`.
The MCP-tools registry and its `mcp-tools.json` projection moved to
Phase 7, where the engine first owns the registry (see *Scope note*
below).

> **Superseded by the catalog+manifest redesign.** This phase shipped
> two generated files — `instructions-files.json` ("wire shape") and
> `instructions-files-metadata.json` ("internal") — under the original
> *wire ≠ internal split* reading of P3. That reading fused three
> independent layers and silently dropped the curatorial taxonomy
> (categories / `label` / `activationFlags`). The corrected design
> (see `§ P3 — three decoupled representations` and `§ Resource
> manifests`) replaces those two files with a **hand-authored**
> `instructions-catalog.json` (curatorial layer, tracked in source)
> and a **build-generated** `instructions-manifest.json` (per-file
> facts). The commit ladder below is preserved as historical record;
> the redesign is tracked on
> `features/fix-metadata-vs-manifest-design`. Read the Phase 5 prose
> that follows as the *original* plan — the catalog+manifest model
> overrides it wherever they disagree.

| # | Commit subject | State |
|---|---|---|
| 1 | `refactor(engine): copy instruction corpus into engine host` | DONE |
| 2 | `feat(build-tasks): scaffold build task projects` | DONE |
| 3 | `feat(build-tasks): generate instructions manifest` | DONE |
| 4 | `refactor(instructions): add shared parser project` | DONE |
| 5 | `refactor(instructions): replace MSBuild task with console app generator` | DONE |
| 6 | `feat(instructions-manifest-gen): emit instructions-files-metadata.json` | DONE |
| 7 | `test(instructions): cover corpus round-trips and applyTo parser` | DONE |
| 8 | `docs(plan): mark Phase 5 complete` | DONE |

**Goal**: a single build-time pass over `src/AutoContext.Engine/Instructions/`
produces both `Resources/instructions-files.json` (wire shape) and
`Resources/instructions-files-metadata.json` (engine-internal
indices). The `applyTo` parser ships in `AutoContext.Instructions.Parser`,
parses only, and is round-trip-verified per fixture.

**Design anchors**: `§ Resource manifests`,
`§ applyTo` matching subsection under `Instructions.*`,
`§ P3` (three decoupled representations), `§ applyTo parser pitfall`.

**Architecture note (as-built)**: the original plan put the builder in
a `netstandard2.0` MSBuild `ITask` library (`AutoContext.Build.Tasks`).
That was replaced by two `net10.0` projects: a shared parser library,
`AutoContext.Instructions.Parser` (frontmatter + `applyTo` parsing —
since extended with a body section index and pure cross-file
`[locator#fragment]` reference resolution; referenced by both the
build-time generator and the engine runtime so one source is compiled
for both), and a console generator,
`AutoContext.Instructions.Manifest.Generator` (AssemblyName
`instructions-manifest-gen`). The engine invokes the generator with
`<Exec>` from `InstructionsManifestGenerator.targets` rather than
loading an `ITask` into MSBuild — this sidesteps the
Full-Framework/Core MSBuild load constraint and lets the generator
target `net10.0` like the rest of the engine. Sequencing is a
`ProjectReference` from `AutoContext.Engine.csproj`
(`ReferenceOutputAssembly=false`); the target runs
`AfterTargets="ResolveProjectReferences" BeforeTargets="CoreCompile"`.

**Scope note (MCP-tools registry moved to Phase 7)**: an earlier
draft folded the MCP-tools registry rename
(`mcp-workers-registry.json` → `mcp-tools-registry.json`) and the
build-time `mcp-tools.json` projection into this phase. That work
moved to Phase 7 — the phase where the engine first *owns* the
registry (`McpTools.List`/`Invoke`). There is no rename: today's
`src/AutoContext.Mcp.Server/mcp-workers-registry.json` stays in
place under its legacy name, serving the still-live MCP server until
Phase 16 deletes that project wholesale. The engine authors its own
`Resources/mcp-tools-registry.json` (and schema, and the projected
`mcp-tools.json`) **fresh, correctly named** in Phase 7 — the same
copy-into-the-engine pattern this phase uses for the instruction
corpus, where the old consumer keeps working untouched and the new
file is born named for the project that owns it.

**Code touch**:
- **Create `AutoContext.Instructions.Parser/` and
  `AutoContext.Instructions.Manifest.Generator/`** — two new `net10.0`
  projects (shared parser library + console generator), each with a
  sibling test project (`AutoContext.Instructions.Parser.Tests`,
  `AutoContext.Instructions.Manifest.Generator.Tests`). Added to
  `AutoContext.slnx` and `build.ps1` in the same change. The
  implementations described below land in these projects as they are
  introduced.
- Curated instruction corpus is **copied** to
  `src/AutoContext.Engine/Instructions/` — the binary host owns the
  side-cars (P5). Today the corpus is co-located with the VS Code
  extension at `src/AutoContext.VsCode/instructions/`; the engine
  binary becomes the owner and the files ship next to the binary
  (resolved at runtime via `AppContext.BaseDirectory`, not embedded
  resources). This is a **copy, not a move**: the VS Code copy stays
  in place and untouched so the extension's existing TS generators,
  `package.json` `chatInstructions` wiring, and packaging keep
  working unchanged. The VsCode original is deleted (and its
  generators/packaging repointed at the engine corpus) in the later
  phase that connects the engine to the VS Code extension — not
  here. The `Instructions/` side-car folder under
  `src/AutoContext.Engine/` is created here (populated with the
  copied corpus); the `Resources/` folder is created in row 4 when
  the build task first writes into it.
- `InstructionsListBuilder` + `InstructionsManifestSerializer` — the
  corpus scan, curatorial validation, and deterministic JSON writer
  live in the `AutoContext.Instructions.Manifest.Generator` console
  exe rather than the engine runtime library, so the build-time
  generator and round-trip verifier ship nothing at runtime. The
  `.targets` file is imported by `AutoContext.Engine.csproj` (binary
  host — the project that owns the output `Resources/` folder) and
  `<Exec>`s the generator, which writes `instructions-files.json`
  (and, in row 6, `instructions-files-metadata.json`) into
  `src/AutoContext.Engine/Resources/`. The frontmatter + `applyTo`
  parser lives in `AutoContext.Instructions.Parser` and is referenced
  by both the generator and the engine runtime via a normal
  `ProjectReference`, so build-time validation and runtime parsing
  compile one source. Today's
  `instructions-files-metadata-generator.ts` (TS) is retired; the
  .NET generator replaces it as the single producer.
- `applyTo` parser: comma-split, brace-expand `{a,b,c}` groups,
  trim whitespace, extract extension set. Round-trip invariant
  (`recomposed == original` modulo whitespace) checked per
  corpus file at build time; a failing round-trip fails the build.

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

**Out of scope**: any runtime projection (Phase 6); the
content-search index seed (Phase 6 uses it but builds the live
index in-memory at startup).

## Phase 6R — Design remediation: catalog + manifest split

**Status**: Completed on branch `features/fix-metadata-vs-manifest-design`,
merged to `dev/autocontext-engine`. The runtime snapshot merge and the
`Instructions.Categories` DTO (see **Out of scope**) resume in Phase 6.

**Why this phase exists**: Phase 6 runtime work was started and then
**stashed** mid-flight when a critical design flaw surfaced. Phase 5's
two generated files — `instructions-files.json` ("wire shape") and
`instructions-files-metadata.json` ("internal") — had converged to
~80% duplicate content because the original P3 reading fused three
independent layers (on-disk format / runtime model / wire shape) and
silently dropped the curatorial taxonomy (categories / `label` /
`activationFlags`) the LM-tools surface still needs
(`list_autocontext_instructions_files` returns `categories`). Building
Phase 6 on top of that shape would have meant porting the flaw into
the runtime and **redoing the work** once it was caught downstream.
This phase corrects the design at the build-time source **before**
Phase 6 resumes. The two files are replaced per the corrected
`§ P3 — three decoupled representations`.

**Goal**: replace the two generated files with one **hand-authored**
curatorial layer (`instructions-catalog.json`, tracked in source) and
one **build-generated** per-file facts manifest
(`instructions-manifest.json`). The generator reads the catalog,
cross-validates it against the corpus, and emits only the manifest.
Phase 6 resumes on top of the corrected files.

**Design anchors**: `§ P3` (three decoupled representations),
`§ Resource manifests` (catalog hand-authored + manifest generated),
`§ RPC surface` (`Instructions.List` + `Instructions.Categories`).

**Code touch**:
- **Generator** (`AutoContext.Instructions.Manifest.Generator`):
  - `InstructionsManifestBuilder` emits `instructions-manifest.json`
    — per-file facts only (`key`, `fileName`, `name`, `version`,
    `description`, `applyTo?`, `extensions?`, `hasChangelog`,
    `contentHash`, `sections[]`). The old wire-shape
    `instructions-files.json` builder is dropped. The manifest carries
    no `categories`/`label`/`activationFlags` (the catalog's) and no
    workspace-state fields (`disabled`/`source` are per-request).
    `alwaysAttached` is **declared** in the catalog's `alwaysAttached`
    array (the single source of truth) and derived by the engine at
    merge time, not baked into the manifest.
  - `InstructionsCatalogReader` reads the hand-authored
    `instructions-catalog.json` and cross-validates against the
    corpus, all rules build-FATAL with an
    `[instructions-catalog.json] …` message: **(A)** every catalog
    entry resolves to a real corpus file; **(B2)** every corpus file
    has a catalog entry **except** the always-attached files (`copilot`,
    `autocontext` — matching the legacy TS exemption); **(membership)**
    every category-membership string resolves to a declared category.
  - Update `Program.cs` args (corpus dir + catalog path + manifest
    output path), `InstructionsManifestGenerator.targets` `<Exec>`,
    the DI registrations, serializers, and JSON source-gen contexts
    to the new shape.
- **Catalog authoring**: author
  `src/AutoContext.Engine/Resources/instructions-catalog.json` by
  porting the TS `src/AutoContext.VsCode/resources/instructions-files.json`
  (categories + `label` + membership + `activationFlags`). Remove it
  from `src/AutoContext.Engine/.gitignore` — the catalog is tracked
  source; only the generated `instructions-manifest.json` stays
  ignored — and wire its copy-to-output.

**Tests**:
- Catalog validation: rules **A** / **B2** / **membership** each fail
  the build with the `[instructions-catalog.json] …` message on a
  crafted bad fixture; the real catalog passes.
- Manifest shape: emitted `instructions-manifest.json` carries the
  per-file facts and **none** of the catalog-only fields; section maps
  and parsed `applyTo` extension sets round-trip.
- Deterministic, byte-stable manifest output (no spurious rewrites
  when inputs are unchanged).

**Out of scope** (these land as Phase 6 resumes, on top of the
corrected files):
- The runtime `InstructionsManifestSnapshot` merge + projection (the
  stashed Row 2 work, rebuilt to the new shape).
- The `Instructions.Categories` wire DTO in
  `AutoContext.Engine.Protocol` (depends on the Row 1 envelope work).

## Phase 6 — Instructions corpus runtime + projection

**Status**: Started, then **paused** — the Row 2 runtime corpus-load
work was stashed pending **Phase 6R** (design remediation). Resumes on
the corrected catalog + manifest shape.

| # | Commit subject | State |
|---|---|---|
| 1 | `feat(protocol): add Instructions.* wire DTOs` | DONE |
| 2 | `feat(engine-core): load instructions corpus snapshot on startup` | DONE |
| 3 | `feat(engine-core): add InstructionsOverrideWatcher with debounced reload` | Not started |
| 4 | `feat(engine): serve Instructions.List over rpc` | Not started |
| 5 | `feat(engine-core): add InstructionsFileBodyProjector with disabled-rule filter and tag strip` | Not started |
| 6 | `feat(engine): serve Instructions.Get and GetAll over rpc` | Not started |
| 7 | `feat(engine): serve Instructions.GetAlwaysAttached over rpc` | Not started |
| 8 | `feat(engine): serve Instructions.GetRaw with bundled/override/active source` | Not started |
| 9 | `feat(engine-core): add InstructionsContentIndex seeded from metadata` | Not started |
| 10 | `feat(engine): serve Instructions.SearchContent over rpc` | Not started |
| 11 | `feat(engine-core): add Instructions.Subscribe events stream with snapshot-on-subscribe` | Not started |
| 12 | `feat(engine-core): rebroadcast Instructions.Subscribe on Config.Subscribe changes` | Not started |
| 13 | `feat(engine-core): warn when an override is older than its bundled file` | Not started |
| 14 | `test(engine): integration test for instructions projection and invalidation over rpc` | Not started |
| 15 | `docs(plan): mark Phase 6 complete` | Not started |

**Goal**: engine answers every `Instructions.*` RPC from in-memory
snapshots, applies per-request projection (disabled rules filtered,
`[INSTxxxx]` stripped, overrides resolved), invalidates cleanly via
`Config.Subscribe`, and exposes content search.

**Design anchors**: `§ RPC surface` (`Instructions.*`),
`§ P2` (discriminated envelopes), `§ P3` (three decoupled
representations — disk catalog+manifest vs. runtime snapshot vs.
wire), `§ P9` (snapshot-immutable),
`§ alwaysAttached pitfall`, `§ Instructions.Get distinguishes disabled
from not-found pitfall`, `§ Override survival across upgrades`
pitfall.

**Code touch**:
- `AutoContext.Engine.Core/Features/Instructions/`:
  - `InstructionsManifestService` — on startup, merge the
    hand-authored `instructions-catalog.json` (categories, `label`,
    membership, `activationFlags`) with the build-generated
    `instructions-manifest.json` (per-file facts) into one immutable
    `InstructionsManifestSnapshot`; re-project per request.
  - `InstructionsFileBodyProjector` — disabled-rule filter,
    `[INSTxxxx]` tag strip, override resolution.
  - `InstructionsContentIndex` — in-memory content search seeded
    from the section/body facts in `instructions-manifest.json`, hot
    across queries, invalidated on corpus reload.
  - `InstructionsOverrideWatcher` — `FileSystemWatcher` on
    `<workspace>/.github/instructions/` with the same debounce shape
    Phase 3 introduced.
- Handlers: `Instructions.List`, `Categories`, `Get`, `GetAll`,
  `GetAlwaysAttached`, `GetRaw` (with
  `opts.source: "bundled"|"override"|"active"`), `SearchContent`,
  `Subscribe`. Discriminated envelopes per `§ P2`. `List` rows carry
  `label` + category `membership` + computed `disabled`/`source`;
  `Categories` returns the taxonomy definitions (`[{name,
  description}]`) from the catalog, static/cached. `activationFlags`
  is engine-internal — it is evaluated against workspace state to
  derive `disabled` and is never serialised on the wire.
- `Config.Subscribe` consumer that re-evaluates `disabled` flags and
  rebroadcasts on `Instructions.Subscribe`.
- Override-mtime-vs-bundled-mtime warning (the *override survival*
  pitfall).

**Tests**:
- `List` returns every bundled + override file; disabled rows carry
  `disabled: true`; `alwaysAttached` flag correctly reflects YAML
  frontmatter; rows carry `label` + category `membership` from the
  catalog.
- `Categories` returns the catalog taxonomy definitions in
  deterministic order; every `membership` string on a `List` row
  resolves to a returned category.
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

## Phase 7 — MCP tool catalog, dispatch, and worker manager

**Status**: Not started.

**Goal**: engine absorbs today's `AutoContext.Mcp.Server` worker
dispatcher. `McpTools.List` and `McpTools.Invoke` answer over the
`rpc` pipe; the MCP-server-only role over stdio comes in Phase 11.
Workers are spawned by the engine via the same lazy
`ensureRunning(workerId)` pattern in use today. The engine also
becomes the owner of the MCP-tools registry, authoring
`mcp-tools-registry.json` (its schema, and the projected
`mcp-tools.json`) **fresh** under its own `Resources/` rather than
renaming today's `AutoContext.Mcp.Server` copy.

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
- **Author the MCP-tools registry fresh under the engine.**
  `Resources/mcp-tools-registry.json` and its
  `mcp-tools-registry.schema.json` are created under
  `src/AutoContext.Engine/Resources/` with their end-state names —
  **not** renamed or moved from today's
  `src/AutoContext.Mcp.Server/mcp-workers-registry.json`. That legacy
  file stays in place, untouched under its old name, serving the
  still-live `AutoContext.Mcp.Server` until Phase 16 deletes the whole
  project (the same copy-into-the-engine pattern Phase 5 used for the
  instruction corpus — the old consumer keeps working under the old
  name; the engine's copy is born correctly named in the project that
  owns it). `McpToolsRegistryLoader` reads it from `Resources/` via
  `AppContext.BaseDirectory`; `McpToolsRegistrySchemaValidator`
  validates it against the embedded schema at both build time and
  load time.
- Build-time projection of `mcp-tools.json` (wire shape only;
  runtime projection applies the disabled-state filter) emitted into
  `src/AutoContext.Engine/Resources/` from the registry above.
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
- `mcp-tools-registry.json` schema-validates at build time; a
  malformed registry fails the build.
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

**Status**: Not started.

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
- Slow `Logs.Tail*` subscriber is dropped with the terminal
  `dropped` frame; the file sink and other subscribers keep going.
- Worker stderr (a print that bypasses the logger) shows up under
  `worker.<id>.engine.stderr` in the worker's log file.

**Out of scope**: any on-disk worker spool — there isn't one, by
design.

## Phase 9 — Discovery

**Status**: Not started.

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

**Status**: Not started.

**Goal**: engine accepts the agent-loop notifications hooks fire
(`SubagentStarted`/`SubagentStopped`/`Compacted`/`ToolUsed`/`TurnEnded`)
and re-broadcasts them on `Agent.Events.Subscribe`. UX-only;
fire-and-forget; lost events tolerable (per the design).

**Design anchors**: `§ RPC surface` (`Agent.*`), `§ P6` (subscription
shape), `§ P10` (cross-process fan-out).

**Code touch**:
- `AutoContext.Engine.Core/Agent/AgentEventFrameStream` over a shared
  `Infrastructure/Events/Broadcaster<AgentEvent>` — same
  per-subscriber bounded-buffer / slow-subscriber-drop discipline
  Phase 2 introduced.
- The five notification handlers; in-memory per-session histogram for
  `ToolUsed` (consumed by `Diagnostics.Run` in a later out-of-scope
  release).

**Tests**:
- Notification → broadcast round-trip per event family.
- Slow subscriber on `Agent.Events.Subscribe` is dropped; producer
  is never back-pressured.
- Two clients subscribed concurrently see the same envelope sequence.

**Out of scope**: hook script integration (Phase 15);
`Diagnostics.Run` consumer.

## Phase 11 — MCP-server-only role

**Status**: Not started.

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

**Status**: Not started.

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
    `Engine.Lifecycle`, `Engine.RegistryEntries`,
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
  `AutoContext.Nodejs.Core/src/pipes/`, moved as part of the
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
- Slow-subscriber on the client side disconnects with `dropped`
  rather than back-pressuring the engine.
- Engine refusal on protocol-version mismatch surfaces as a typed
  error on both clients.

**Out of scope**: extension consuming the client (Phase 14); hooks
consuming the client (Phase 15); CLI verb implementations
(`autocontext-cli.md`, separate plan).

## Phase 13 — Distribution and packaging

**Status**: Not started.

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

**Status**: Not started.

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
- `build.ps1 Compile -Smoke` (the VS Code extension smoke test) runs
  end-to-end: extension activates, spawns the engine, tree view
  populates, an instruction toggle round-trips.
- Cross-window scenario: two VS Code windows on the same workspace
  spawn two engines; toggles in one window reach the other through
  the cross-instance `.autocontext.json` path (Phase 3 contract).

**Out of scope**: hook scripts (Phase 15); `Mcp.Server` deletion
(Phase 16).

## Phase 15 — Agent-plugin hook migration

**Status**: Not started.

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

**Status**: Not started.

**Goal**: the standalone MCP-server project is gone. The MCP host
servers manifest (`servers.json`) points at
`autocontext-engine --mcp-server with-stdio`. Tests fold into
`AutoContext.Engine.Core.Tests`.

**Design anchors**: `§ What the engine absorbs from today's topology`,
`§ Test-project layout`.

**Code touch**:
- Delete `src/AutoContext.Mcp.Server/` and
  `tests/AutoContext.Mcp.Server.Tests/`.
- Tests worth keeping move into `AutoContext.Engine.Core.Tests`
  (the schema-validation tests, the manifest-loader tests, the
  envelope-composition tests).
- `servers.json` rewritten: the only entry is `autocontext-engine`
  with the MCP-server-only role argv; worker entries are removed
  (the engine spawns workers itself now).
- The engine-owned `mcp-tools-registry.json` was authored fresh
  under `AutoContext.Engine/Resources/` in Phase 7 (not moved — the
  old `mcp-workers-registry.json` stayed in `AutoContext.Mcp.Server/`
  untouched under its legacy name); deleting `AutoContext.Mcp.Server/`
  here removes that legacy registry and schema along with the rest of
  the project.
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
   Phase 4 ports the flag set unchanged — same flag names, same
   globs, same regex patterns, same activation edges — but folds the
   two identical content arrays (`npmContentRules`,
   `dotnetContentRules`) into one `ContentScans` table whose rows
   group a manifest's file selectors with its `ContentPatternRule`
   probes, so a new platform is a data row rather than a new type.
   The ~60-flag contract in the design doc and these tables are the
   same set; no separate fixture extraction is needed. The per-flag tests use the
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
