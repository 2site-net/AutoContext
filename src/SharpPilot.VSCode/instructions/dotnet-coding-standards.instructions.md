---
description: "Use when writing .NET code: design guidelines, naming conventions, input validation, file organization, and API design."
applyTo: "**/*.{cs,fs,vb}"
---
# Coding Standards

- **Do** follow .NET design guidelines and common conventions unless noted below.
- **Do** use current .NET features when they deliver measurable performance gains.
- **Do** validate inputs at system boundaries.
- **Do** use `ArgumentException.ThrowIf*`, `ArgumentNullException.ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIf*` and similar static throw helpers for precondition checks.
- **Do** give descriptive names that convey intent — prefer `retriesRemaining` over `n` and `GetUserById` over `GetUser`.
- **Do** prefix interfaces with `I` (e.g., `IMyType`).
- **Do** suffix extension classes with `Extensions` (e.g., `MyTypeExtensions`).
- **Do** suffix async methods with `Async` (e.g., `GetDataAsync()`).
- **Do** keep a single type per file and name the file after that type (e.g., `User.cs` for `User` class).
- **Do** place each type in a folder that reflects its role and namespace, naming the folder after the namespace.
- **Do** comment the non-obvious "why" — algorithms, design trade-offs, and anything a future reader might find surprising — not the "what".
- **Do** run `dotnet format`, then `dotnet build`, then `dotnet test` after making changes — fix build errors before addressing test failures.
- **Do** prefer built-in formatting rules (e.g., `IDEXXXXX`, `dotnet`, `csharp`) over StyleCop when they provide sufficient coverage.
- **Don't** expose internal implementation details in public APIs; use interfaces or abstractions instead.
- **Don't** call `Console.*` or `Debug.*`; use `ILogger` or `Serilog` in production and `ITestOutputHelper` in tests.
