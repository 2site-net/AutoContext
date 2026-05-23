# Backlog

Findings deferred during reviews — bugs, cleanups, missing tests, and
doc gaps that we don't want to lose but don't want to smuggle into an
unrelated commit either.

## How to use this file

- **Add an entry** the moment a review surfaces something worth
  tracking that isn't in scope for the current commit / phase. Don't
  fix it inline; log it here and keep moving.
- **Newest first.** Append new entries at the top of the *Open*
  section. The chronological order is the audit trail.
- **One entry per finding**, using the shape below.
- **When it lands**, move the entry verbatim to the *Resolved* section
  at the bottom and append the commit SHA + date. Do not edit the
  body — the original framing is the historical record.

### Entry template

```markdown
## <short title>

- **Found**: <YYYY-MM-DD> during <context — e.g. "Phase 0 commit #1 review">
- **Severity**: bug | cleanup | missing-test | doc
- **Location**: `path/to/file.cs` ~Lxx (and any peer call sites)
- **Symptom**: …
- **Fix shape**: …
- **Lands**: <which phase / branch / "anytime after Phase N">
```

---

## Open

## `get_autocontext_instructions_file` MCP tool returns stale instruction body

- **Found**: 2026-05-23 during review of `tests/support/` refactor on
  `dev/autocontext-engine`.
- **Severity**: bug.
- **Location**: `src/AutoContext.Mcp.Server/**` — whatever path serves
  `get_autocontext_instructions_file` (and likely the shared cache
  feeding `search_autocontext_instructions_files_by_content` /
  `…_by_metadata`).
- **Symptom**: calling
  `get_autocontext_instructions_file({ name: "testing.instructions.md" })`
  returned a body **missing INST0014/INST0015/INST0016** (the
  `Support/` folder layout, the `Fake<TypeName>` / `<TypeName>Test<RoleName>`
  naming rules, and the artifact-kind-folders prohibition), even though
  those rules are present in the on-disk source at
  `src/AutoContext.VsCode/instructions/testing.instructions.md`.
  This caused a code review to flag a refactor as "diverging from
  convention" when in fact the refactor followed INST0014–INST0016
  exactly. The `description` and `sections[]` indexes in the
  metadata-search response also reflected the older content.
- **Fix shape**: audit the build/load pipeline that materialises
  instruction-file bodies for the MCP server — likely an instructions
  manifest, metadata JSON, or section cache produced by
  `build.ps1`/`Prepare` that wasn't regenerated when
  `testing.instructions.md` was last edited (or a generator step that
  drops bullets it can't parse). Add a guard that the served body's
  content hash matches the on-disk file at server-start, and either
  fail loudly or re-read from disk when they diverge. Add a regression
  test that fetches `testing.instructions.md` via the MCP tool and
  asserts INST0014/INST0015/INST0016 are present in the returned body.
- **Lands**: anytime — this silently misleads any agent that relies on
  the MCP tools instead of opening the file directly, so worth
  prioritising before another reviewer hits the same trap.

## Harden `PipeListener.Bind()` cleanup contract

- **Found**: 2026-05-16 during Phase 1 commit #6 review
  (`feat(engine-core): add LifecycleService four-pipe accept loops`).
- **Severity**: bug (latent).
- **Location**:
  - `src/AutoContext.Framework.Pipes/PipeListener.cs` ~L83-L93
    (`Bind()` constructs a `NamedPipeServerStream` then passes it to
    `new BoundPipeListener(...)` ~L93 without a try/catch around the
    handoff).
  - `src/AutoContext.Engine.Core/Lifecycle/LifecycleService.cs` ~L212
    (`BindAll`) is the caller whose cleanup comment ("Bind throws
    before the listener owns any OS resources") banks on the
    invariant being true.
- **Symptom**: if anything between `new NamedPipeServerStream(...)`
  and `return new BoundPipeListener(...)` throws — today only the
  `BoundPipeListener` constructor itself, but any future addition
  (logging, instrumentation, options validation) would qualify — the
  raw `NamedPipeServerStream` is leaked and the OS pipe handle stays
  bound until GC finalisation. `LifecycleService.BindAll` then
  unwinds *previously*-bound listeners cleanly but never disposes
  the half-bound stream of the failing kind.
- **Fix shape**: wrap the post-construct work in `PipeListener.Bind()`
  in a `try { ... } catch { pipe.Dispose(); throw; }` so the
  invariant the call-site comment claims actually holds, regardless
  of what gets added between construction and return. Optionally
  tighten the `LifecycleService.BindAll` comment to reference the
  contract rather than implementation detail.
- **Lands**: anytime — independent `fix(pipes): dispose
  NamedPipeServerStream when BoundPipeListener handoff throws`
  commit. Pair with a unit test that injects a throwing
  `BoundPipeListener`-ish stand-in (or simulate via reflection) to
  prove the stream is disposed on the failure path.

## Fix `mcp-tools-tree-view.test.ts` for the `mcpServerNode` root row

- **Found**: 2026-05-14 during Phase 0 commit #5 smoke run
  (`refactor(ts): rename Framework.Web to Nodejs.Core`).
- **Severity**: bug (test-only — the failure is pre-existing on
  `HEAD` `03e65a0`, unrelated to the rename).
- **Location**:
  - `src/AutoContext.VsCode/tests/smoke-tests/mcp-tools-tree-view.test.ts`
    ~L6-L29 (and every other test in the suite that iterates `roots`
    without filtering — L31, L48, L73, L88, L121, L150, L173).
  - Drift root cause: `src/AutoContext.VsCode/src/mcp-tools-tree-provider.ts`
    `getChildren(undefined)` now prepends an `mcpServerNode` (the
    MCP server status row added by `7b9ffc7 feat(vscode): add MCP
    server status row`), so the first root is no longer an
    `mcpTopCategoryNode`.
- **Symptom**: 2 failing assertions:
  1. `should return root nodes from the tree view` — `roots.every(r =>
     r.kind === 'mcpTopCategoryNode')` is false because the first
     root is `mcpServerNode`.
  2. `should contain sub-categories under top categories` — the
     iteration hits the `mcpServerNode` root, asks for its children
     (empty), and fails the "at least one sub-category" assertion.
- **Fix shape**: in each smoke test, filter the roots collection
  down to `mcpTopCategoryNode` entries before iterating
  (`const topCategories = roots.filter(r => r.kind ===
  'mcpTopCategoryNode')`), and assert that **every top-category root**
  has kind `mcpTopCategoryNode` plus that exactly one `mcpServerNode`
  is present (don't loosen the existing kind check — split it).
- **Lands**: anytime — separate `fix(smoke-tests): account for
  mcpServerNode tree root` commit on `features/framework-restructure`
  or `main`, independent of Phase 0.

## Refresh `README.md` + `docs/architecture.md` for Phase 0 topology

- **Found**: 2026-05-14 during Phase 0 commit #2 review (`refactor(mcp):
  fold IMcpTask into Framework.Workers`).
- **Severity**: doc.
- **Location**:
  - `README.md` ~L33 — Repository Structure block still lists
    `src/AutoContext.Mcp.Abstractions/` as a real folder, and the
    `src/AutoContext.Framework/` line still describes the pre-split
    monolith (Pipes/Logging/Workers/Hosting all under one project).
  - `docs/architecture.md` ~L474 — Projects section still bullets
    `AutoContext.Mcp.Abstractions` as a live project, describes
    `AutoContext.Framework` as a single project containing
    `Pipes/Hosting/Logging/Workers`, and references the old
    `HealthMonitorClient` instead of `WorkerHealthMonitorService`.
- **Symptom**: both files describe the pre-Phase-0 topology. After
  commits #1 (Framework split into four sub-projects + renames) and
  #2 (Mcp.Abstractions folded into Framework.Workers), the prose is
  factually wrong on which projects exist and what lives where.
- **Fix shape**: rewrite the Repository Structure block in
  `README.md` and the Projects section in `docs/architecture.md` to
  reflect the post-Phase-0 project graph — `AutoContext.Framework.*`
  sub-projects, no `Mcp.Abstractions`, current type names
  (`WorkerHealthMonitorService` etc.). Keep the depth/tone consistent
  with the surrounding prose; do not retrofit Phase 1+ topology.
- **Lands**: Phase 0 commit #6 (`docs(plan): correct Worker.Shared
  fold scope`) — widen its remit to cover both files — or a dedicated
  `docs: refresh README + architecture for Phase 0 topology` commit
  immediately after the Phase 0 ladder lands.

## Wire `ILogger` through `WorkerHealthMonitorService`

- **Found**: 2026-05-14 during Phase 0 commit #1 review (file
  inherited from old `HealthMonitorClient`).
- **Severity**: bug.
- **Location**:
  - `src/AutoContext.Framework.Workers/WorkerHealthMonitorService.cs`
    — ctor validates `ILogger<WorkerHealthMonitorService> logger` with
    `ArgumentNullException.ThrowIfNull(logger)` but never stores or
    forwards it.
  - Call sites that resolve and pass a real logger for nothing:
    - `src/AutoContext.Mcp.Server/Program.cs` ~L146-L149
    - `src/AutoContext.Framework.Workers/WorkerHostBuilderExtensions.cs` ~L112-L115
- **Symptom**: the composed `PipeTransport` and `PipeKeepAliveClient`
  are wired to `NullLogger.Instance`, so every diagnostic from the
  keep-alive connect/retry path is silently dropped — despite the
  type's XML remarks promising "failures are logged and swallowed".
- **Fix shape**: store the injected logger on a private field and
  pass it (or typed children minted from an injected `ILoggerFactory`)
  to `PipeTransport` and `PipeKeepAliveClient` instead of
  `NullLogger.Instance`. Add a unit test that dials a non-existent
  pipe and asserts the logger received at least one entry at
  Warning/Error naming the pipe.
- **Lands**: dedicated `fix(workers): wire ILogger through
  WorkerHealthMonitorService` commit on a branch off `main` after
  Phase 0 merges — or rolled into the deferred test-style sweep when
  `WorkerHealthMonitorServiceTests` is reworked, since the rewritten
  test would give ready-made coverage.

---

## Resolved

<!-- Move entries here when their fix commit lands. Append the SHA + date. -->
