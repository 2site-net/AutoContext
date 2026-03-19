---
description: "Use when writing, reviewing, or refactoring MSTest tests, test initialization, or MSTest-specific APIs in .NET."
applyTo: "**/*Tests*.{cs,vb,razor}"
---
# MSTest Guidelines

- **Do** use **MSTest v4** with the `[TestClass]` / `[TestMethod]` attribute model.
- **Do** use `[TestMethod]` + `[DataRow]` for parameterised tests instead of duplicating test methods.
- **Do** use `Assert.ThrowsExactly<T>()` / `await Assert.ThrowsExactlyAsync<T>()` to verify expected exceptions.
- **Do** use `Assert.That` for expression-based assertions — it inspects the expression result and produces clear failure messages.
- **Do** use `[TestInitialize]` and `[TestCleanup]` for per-test setup/teardown; use `[ClassInitialize]` and `[ClassCleanup]` only for expensive shared resources.
- **Do** use `[AssemblyInitialize]` and `[AssemblyCleanup]` sparingly — only for truly global setup like test database creation.
- **Do** use `TestContext` to access test metadata and output diagnostics — be aware that test-run properties are not available during `[AssemblyInitialize]` or `[ClassInitialize]`.
- **Do** use `[Timeout]` at method level (not class level) to guard against hangs in async tests.
- **Do** use string interpolation in assertion messages — the `message` + `object[]` overloads were removed in v4.
- **Don't** use `[ExpectedException]` — it was removed in v4; use `Assert.ThrowsExactly<T>` instead.
- **Don't** share mutable state between tests via static fields — MSTest does not guarantee execution order.
