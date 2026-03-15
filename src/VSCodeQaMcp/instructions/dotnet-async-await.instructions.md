---
description: "Use when writing async .NET code, Task/ValueTask APIs, CancellationToken, IAsyncEnumerable, IAsyncDisposable, or ConfigureAwait patterns."
applyTo: "**/*.{cs,fs,vb}"
---
# Async / Await Guidelines

- **Do** write true `async`/`await` code, don't mix sync and async code; follow the async all the way down.
- **Do** add an optional `CancellationToken ct = default` as the final parameter in public async APIs.
- **Do** use `IAsyncEnumerable<T>` for streaming operations (e.g., `await foreach (var row in repo.GetRowsAsync()) {}`).
- **Do** use `ValueTask` only when best practices permit and profiling shows a measurable benefit over `Task`.
- **Do** implement `IAsyncDisposable` for async cleanup.
- **Do** add `.ConfigureAwait(false)` in library or other non-UI code to avoid deadlocks on captured contexts except in xUnit tests (xUnit1030).
- **Don’t** use `async void` except for event handlers—unobserved exceptions crash the process.