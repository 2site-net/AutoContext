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
