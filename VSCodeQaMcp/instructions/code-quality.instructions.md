---
description: "Use when building, formatting, testing, or maintaining C# code quality: dotnet workflow, dead code, build hygiene, and logging practices."
applyTo: "**/*.cs"
---
# Code Quality & Development Workflow

- **Do** run `dotnet format`, then `dotnet build`, then `dotnet test` after making changes — fix build errors before addressing test failures.
- **Do** prefer simple, readable implementations over premature optimization (e.g., avoid complex LINQ queries when a simple loop suffices).
- **Do** distinguish dead code (never called) from test-only code — verify test utilities actually serve a clear purpose before removing them.
- **Do** fix one category of errors completely before moving to the next.
- **Do** validate that all tests pass before considering work complete.
- **Do** prefer built-in formatting rules (e.g., `IDEXXXXX`, `dotnet`, `csharp`) over StyleCop when they provide sufficient coverage.
- **Don't** call `Console.*` or `Debug.*`; use `ILogger` or `Serilog` in production and `ITestOutputHelper` in tests.
