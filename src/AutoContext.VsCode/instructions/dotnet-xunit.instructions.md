---
name: "dotnet-xunit (v1.0.0)"
description: "Use when writing, reviewing, or refactoring xUnit tests, test doubles, or xUnit-specific APIs in .NET."
applyTo: "**/*Tests*.{cs,fs,vb,razor}"
---
# xUnit Guidelines

- [INST0001] **Do** use **xUnit v3** with `[Fact]` / `[Theory]` attributes and constructor injection for test setup.
- [INST0002] **Do** use `[Theory]` + `[InlineData]` for simple parameterised cases and `[MemberData]` or `[ClassData]` for complex or shared test data.
- [INST0003] **Do** use `await Assert.ThrowsAsync<T>()` for async exceptions.
- [INST0004] **Do** use `Assert.Multiple()` when a test needs more than one assertion, so all failures are reported together.
- [INST0005] **Do** use `Assert.Skip()` to conditionally skip tests at runtime instead of commenting them out or using conditional compilation.
- [INST0006] **Do** use `IAsyncLifetime` for async setup/teardown — `InitializeAsync` and `DisposeAsync` return `ValueTask` in v3.
- [INST0007] **Do** use fixtures at the right scope: `IClassFixture<T>` for a single class, `ICollectionFixture<T>` for multiple classes, and `[AssemblyFixture]` for assembly-wide shared state.
- [INST0008] **Do** log diagnostics via `ITestOutputHelper` (in `Xunit.Sdk`) or access test metadata via `TestContext`.
- [INST0009] **Don't** call `.ConfigureAwait(...)` inside test methods (xUnit1030).
- [INST0010] **Don't** use `async void` test methods — `AsyncTestSyncContext` was removed in v3; always return `Task` or `ValueTask`.
