---
name: "dotnet-coding-standards (v1.0.0)"
description: "Apply when writing or reviewing .NET code (design guidelines, naming, input validation, file organization, API design)."
applyTo: "**/*.{cs,fs,vb}"
---

# .NET Coding Standards Instructions

## MCP Tool Validation

After editing or generating any C# source file, call the
`analyze_csharp_code` MCP tool on the changed source. Pass the file
contents as `content` and the file's absolute path as `originalPath`.
For test files, also pass the production type's namespace as
`originalNamespace` and the test file path as `comparedPath`. Treat
any reported violation as blocking — fix it before reporting the work
as done.

## Rules

### General

- [INST0001] **Do** follow .NET design guidelines and common conventions unless noted below.
- [INST0002] **Do** use current .NET features when they deliver measurable performance gains.
- [INST0003] **Do** prefer source-generated APIs over their legacy runtime-reflection counterparts — `[LibraryImport]` over `[DllImport]`, `[GeneratedRegex]` over `new Regex(…)`, `[LoggerMessage]` over manual log delegates, and `[JsonSerializable]` source-generated contexts over reflection-based `System.Text.Json` — they are trim- and AOT-safe, catch errors at compile time, and eliminate runtime code-generation overhead.
- [INST0004] **Do** validate inputs at system boundaries.
- [INST0005] **Do** use `ArgumentException.ThrowIf*`, `ArgumentNullException.ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIf*` and similar static throw helpers for precondition checks.
- [INST0006] **Don't** catch `System.Exception` (or `System.SystemException`) unless you immediately rethrow it — only catch the most specific exception type you can meaningfully handle.
- [INST0007] **Don't** use an empty or log-only `catch` block when you can use an exception filter (`when`) instead — `catch (IOException e) when (e.HResult == ...)` preserves the original stack trace and avoids swallowing unrelated exceptions.
- [INST0008] **Don't** call `Console.*` or `Debug.*`; use `ILogger` or `Serilog` in production and `ITestOutputHelper` in tests.

### Naming Conventions

- [INST0009] **Do** give descriptive names that convey intent — prefer `retriesRemaining` over `n` and `GetUserById` over `GetUser`.
- [INST0010] **Do** prefix interfaces with `I` (e.g., `IMyType`).
- [INST0011] **Do** suffix attribute types with `Attribute` (e.g., `SerializableAttribute`).
- [INST0012] **Do** suffix extension classes with `Extensions` (e.g., `MyTypeExtensions`).
- [INST0013] **Do** suffix async methods with `Async` (e.g., `GetDataAsync()`).
- [INST0014] **Do** use a singular noun for non-flags enum types and a plural noun for flags enum types (e.g., `FileMode` vs `FileAttributes`).
- [INST0015] **Do** name Boolean properties with an affirmative phrase — optionally prefix with `Is`, `Can`, or `Has` when it adds clarity (e.g., `IsEnabled`, `CanSeek`, `HasChildren`).

### File & Project Organization

- [INST0016] **Do** keep a single type per file and name the file after that type (e.g., `User.cs` for `User` class).
- [INST0017] **Do** place each type in a folder that reflects its role and namespace, naming the folder after the namespace.
- [INST0018] **Do** comment the non-obvious "why" — algorithms, design trade-offs, and anything a future reader might find surprising — not the "what".
- [INST0019] **Do** run `dotnet format`, then `dotnet build`, then `dotnet test` after making changes — fix build errors before addressing test failures.
- [INST0020] **Do** prefer built-in formatting rules (e.g., `IDEXXXXX`, `dotnet`, `csharp`) over StyleCop when they provide sufficient coverage.

### Immutability & API Surface

- [INST0021] **Do** prefer `readonly` fields and `init`-only properties; use `record` for data-carrying types — immutability eliminates accidental mutation bugs.
- [INST0022] **Do** return `IReadOnlyCollection<T>` or `IEnumerable<T>` from public APIs instead of `List<T>` or other mutable concrete types — callers should not depend on mutability you did not intend.
- [INST0023] **Don't** expose internal implementation details in public APIs; use interfaces or abstractions instead.
