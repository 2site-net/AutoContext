---
description: "Use when writing async .NET code, Task/ValueTask APIs, CancellationToken, IAsyncEnumerable, IAsyncDisposable, or ConfigureAwait patterns."
applyTo: "**/*.{cs,fs,vb}"
version: "1.0.0"
---
# Async / Await Guidelines

- [INST0001] **Do** write true `async`/`await` code, don't mix sync and async code; follow the async all the way down.
- [INST0002] **Do** add an optional `CancellationToken ct = default` as the final parameter in public async APIs.
- [INST0003] **Do** use `IAsyncEnumerable<T>` for streaming operations (e.g., `await foreach (var row in repo.GetRowsAsync()) {}`).
- [INST0004] **Do** use `ValueTask` only when best practices permit and profiling shows a measurable benefit over `Task`.
- [INST0005] **Do** implement `IAsyncDisposable` for async cleanup.
- [INST0006] **Do** add `.ConfigureAwait(false)` in library or other non-UI code to avoid deadlocks on captured contexts except in xUnit tests (xUnit1030).
- [INST0007] **Don't** use `async void` except for event handlers—unobserved exceptions crash the process.
- [INST0008] **Don't** block on async code by calling `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on a `Task` — this deadlocks in UI / ASP.NET contexts and defeats all back-pressure from the async pipeline.
