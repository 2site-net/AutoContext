---
description: "Use when writing, reviewing, or refactoring xUnit tests, test doubles, or xUnit-specific APIs in .NET."
applyTo: "**/*Tests*.{cs,razor}"
---
# xUnit Guidelines

- **Do** use **xUnit v3** and **Moq** (only when mocking is truly required).
- **Do** log diagnostics via `ITestOutputHelper` (in `Xunit.Sdk`).
- **Do** use `[Theory]` + `[InlineData]` for parameterised cases and `await Assert.ThrowsAsync<T>()` for async exceptions.
- **Do** use `Assert.Multiple()` when a test needs more than one assertion, so all failures are reported together.
- **Don't** call `.ConfigureAwait(...)` inside test methods (xUnit1030).
