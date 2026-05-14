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
    - `src/AutoContext.Worker.Shared/Hosting/WorkerHostBuilderExtensions.cs` ~L113-L116
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
