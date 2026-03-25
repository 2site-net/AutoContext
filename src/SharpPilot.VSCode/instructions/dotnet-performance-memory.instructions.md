---
description: "Use when benchmarking, optimizing performance, profiling memory, using Span/Memory/ArrayPool/stackalloc, evaluating SIMD/Vector paths, or deciding whether to add instrumentation in .NET."
applyTo: "**/*.{cs,fs,vb}"
---
# Performance & Memory Guidelines

- [INST0001] **Do** prefer simple, readable implementations over premature optimization (e.g., avoid complex LINQ queries when a simple loop suffices).
- [INST0002] **Do** benchmark every non-trivial optimisation first. Keep the tests in a dedicated `*.Benchmarks` project and use `BenchmarkDotNet`.
- [INST0003] **Do** profile allocations before and after every memory change (e.g., `BenchmarkDotNet` + `[MemoryDiagnoser]`, `dotnet-counters`, IDE profiler).
- [INST0004] **Do** use `Span<T>` / `Memory<T>` whenever they cut allocations or clarify buffer handling; in hot paths, prefer them by default.
- [INST0005] **Do** `stackalloc` buffers < 256 bytes in hot paths; keep the span on the stack instead of the heap.
- [INST0006] **Do** benchmark before adopting `ArrayPool<T>.Shared`; if you rent, guard it with `try / finally` and always return.
- [INST0007] **Do** wrap any type with `Dispose()` or `DisposeAsync()` (class or struct) in `using` / `await using` to guarantee cleanup.
- [INST0008] **Do** pin buffers **only** for native interop or across `await`; unpin immediately (`using var h = mem.Pin();` or `fixed { … }`).
- [INST0009] **Do** hide memory details behind an API when it simplifies usage; callers should never manage lifetimes.
- [INST0010] **Do** adopt specialised paths (e.g., `Vector<T>` / SIMD) **only** when the benchmark proves a clear win; otherwise keep the simple loop.
- [INST0011] **Don't** maintain parallel SIMD + fallback code if the gain is negligible.
- [INST0012] **Don't** leave in elaborate metrics or instrumentation that nobody consumes.
- [INST0013] **Don't** rent from `ArrayPool<T>` inside the innermost loop unless a benchmark proves net savings—tiny objects may be cheaper than pool contention.
- [INST0014] **Don't** leave large arrays pinned for long durations.
