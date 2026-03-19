---
description: "Use when writing, reviewing, or refactoring xUnit tests, test doubles, or xUnit-specific APIs in .NET."
applyTo: "**/*Tests*.{cs,vb,razor}"
---
# xUnit Guidelines

- **Do** use **xUnit v3** with `[Fact]` / `[Theory]` attributes and constructor injection for test setup.
- **Do** use `[Theory]` + `[InlineData]` for simple parameterised cases and `[MemberData]` or `[ClassData]` for complex or shared test data.
- **Do** use `await Assert.ThrowsAsync<T>()` for async exceptions.
- **Do** use `Assert.Multiple()` when a test needs more than one assertion, so all failures are reported together.
- **Do** use `Assert.Skip()` to conditionally skip tests at runtime instead of commenting them out or using conditional compilation.
- **Do** use `IAsyncLifetime` for async setup/teardown — `InitializeAsync` and `DisposeAsync` return `ValueTask` in v3.
- **Do** use fixtures at the right scope: `IClassFixture<T>` for a single class, `ICollectionFixture<T>` for multiple classes, and `[AssemblyFixture]` for assembly-wide shared state.
- **Do** log diagnostics via `ITestOutputHelper` (in `Xunit.Sdk`) or access test metadata via `TestContext`.
- **Don't** call `.ConfigureAwait(...)` inside test methods (xUnit1030).
- **Don't** use `async void` test methods — `AsyncTestSyncContext` was removed in v3; always return `Task` or `ValueTask`.
