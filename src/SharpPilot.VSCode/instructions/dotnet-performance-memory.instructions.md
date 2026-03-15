---
description: "Use when benchmarking, optimizing performance, profiling memory, using Span/Memory/ArrayPool/stackalloc, evaluating SIMD/Vector paths, or deciding whether to add instrumentation in .NET."
applyTo: "**/*.{cs,fs,vb}"
---
# Performance & Memory Guidelines

- **Do** prefer simple, readable implementations over premature optimization (e.g., avoid complex LINQ queries when a simple loop suffices).

- **Do** benchmark every non-trivial optimisation first. Keep the tests in a dedicated `*.Benchmarks` project and use `BenchmarkDotNet`.
- **Do** profile allocations before and after every memory change (e.g., `BenchmarkDotNet` + `[MemoryDiagnoser]`, `dotnet-counters`, IDE profiler).
- **Do** use `Span<T>` / `Memory<T>` whenever they cut allocations or clarify buffer handling; in hot paths, prefer them by default.
- **Do** `stackalloc` buffers < 256 bytes in hot paths; keep the span on the stack instead of the heap.
- **Do** benchmark before adopting `ArrayPool<T>.Shared`; if you rent, guard it with `try / finally` and always return.
- **Do** wrap any type with `Dispose()` or `DisposeAsync()` (class or struct) in `using` / `await using` to guarantee cleanup.
- **Do** pin buffers **only** for native interop or across `await`; unpin immediately (`using var h = mem.Pin();` or `fixed { … }`).
- **Do** hide memory details behind an API when it simplifies usage; callers should never manage lifetimes.
- **Do** adopt specialised paths (e.g., `Vector<T>` / SIMD) **only** when the benchmark proves a clear win; otherwise keep the simple loop.
- **Don't** maintain parallel SIMD + fallback code if the gain is negligible.
- **Don't** leave in elaborate metrics or instrumentation that nobody consumes.
- **Don't** rent from `ArrayPool<T>` inside the innermost loop unless a benchmark proves net savings—tiny objects may be cheaper than pool contention.
- **Don't** leave large arrays pinned for long durations.
