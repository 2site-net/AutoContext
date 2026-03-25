---
description: "Use when writing .NET code: design guidelines, naming conventions, input validation, file organization, and API design."
applyTo: "**/*.{cs,fs,vb}"
---
# Coding Standards

- [INST0001] **Do** follow .NET design guidelines and common conventions unless noted below.
- [INST0002] **Do** use current .NET features when they deliver measurable performance gains.
- [INST0003] **Do** prefer source-generated APIs over their legacy runtime-reflection counterparts — `[LibraryImport]` over `[DllImport]`, `[GeneratedRegex]` over `new Regex(…)`, `[LoggerMessage]` over manual log delegates, and `[JsonSerializable]` source-generated contexts over reflection-based `System.Text.Json` — they are trim- and AOT-safe, catch errors at compile time, and eliminate runtime code-generation overhead.
- [INST0004] **Do** validate inputs at system boundaries.
- [INST0005] **Do** use `ArgumentException.ThrowIf*`, `ArgumentNullException.ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIf*` and similar static throw helpers for precondition checks.
- [INST0006] **Do** give descriptive names that convey intent — prefer `retriesRemaining` over `n` and `GetUserById` over `GetUser`.
- [INST0007] **Do** prefix interfaces with `I` (e.g., `IMyType`).
- [INST0008] **Do** suffix attribute types with `Attribute` (e.g., `SerializableAttribute`).
- [INST0009] **Do** suffix extension classes with `Extensions` (e.g., `MyTypeExtensions`).
- [INST0010] **Do** suffix async methods with `Async` (e.g., `GetDataAsync()`).
- [INST0011] **Do** use a singular noun for non-flags enum types and a plural noun for flags enum types (e.g., `FileMode` vs `FileAttributes`).
- [INST0012] **Do** name Boolean properties with an affirmative phrase — optionally prefix with `Is`, `Can`, or `Has` when it adds clarity (e.g., `IsEnabled`, `CanSeek`, `HasChildren`).
- [INST0013] **Do** keep a single type per file and name the file after that type (e.g., `User.cs` for `User` class).
- [INST0014] **Do** place each type in a folder that reflects its role and namespace, naming the folder after the namespace.
- [INST0015] **Do** comment the non-obvious "why" — algorithms, design trade-offs, and anything a future reader might find surprising — not the "what".
- [INST0016] **Do** run `dotnet format`, then `dotnet build`, then `dotnet test` after making changes — fix build errors before addressing test failures.
- [INST0017] **Do** prefer built-in formatting rules (e.g., `IDEXXXXX`, `dotnet`, `csharp`) over StyleCop when they provide sufficient coverage.
- [INST0018] **Do** prefer `readonly` fields and `init`-only properties; use `record` for data-carrying types — immutability eliminates accidental mutation bugs.
- [INST0019] **Do** return `IReadOnlyCollection<T>` or `IEnumerable<T>` from public APIs instead of `List<T>` or other mutable concrete types — callers should not depend on mutability you did not intend.
- [INST0020] **Don't** expose internal implementation details in public APIs; use interfaces or abstractions instead.
- [INST0021] **Don't** catch `System.Exception` (or `System.SystemException`) unless you immediately rethrow it — only catch the most specific exception type you can meaningfully handle.
- [INST0022] **Don't** use an empty or log-only `catch` block when you can use an exception filter (`when`) instead — `catch (IOException e) when (e.HResult == ...)` preserves the original stack trace and avoids swallowing unrelated exceptions.
- [INST0023] **Don't** call `Console.*` or `Debug.*`; use `ILogger` or `Serilog` in production and `ITestOutputHelper` in tests.
