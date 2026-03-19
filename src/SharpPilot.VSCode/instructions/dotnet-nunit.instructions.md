---
description: "Use when writing, reviewing, or refactoring NUnit tests, assertions, or NUnit-specific APIs in .NET."
applyTo: "**/*Tests*.{cs,fs,vb,razor}"
---
# NUnit Guidelines

- **Do** use **NUnit 4** with the constraint-based assertion model (`Assert.That(…, Is.EqualTo(…))`).
- **Do** use `[TestCase]` for inline parameterised tests and `[TestCaseSource]` for complex or shared data.
- **Do** use `Assert.ThrowsAsync<T>(async () => …)` or `Assert.That(async () => …, Throws.TypeOf<T>())` to verify async exceptions.
- **Do** use `Assert.Multiple(() => { … })` so all assertions execute and all failures are reported together.
- **Do** use `[SetUp]` / `[TearDown]` for per-test lifecycle and `[OneTimeSetUp]` / `[OneTimeTearDown]` for expensive shared resources.
- **Do** use `TestContext.Out` or `TestContext.WriteLine` for test diagnostics output.
- **Do** use string interpolation in assertion messages — `Assert.That` overloads with format specification and `params` were removed in NUnit 4.
- **Do** use `[Retry(n)]` only for inherently flaky integration tests — never for unit tests.
- **Do** use `[Category("…")]` to tag slow or integration tests so they can be filtered in CI.
- **Don't** use the classic assertion model (`Assert.AreEqual`) — it was moved to `NUnit.Framework.Legacy.ClassicAssert` in NUnit 4; always prefer the constraint model (`Assert.That`).
- **Don't** use `[ExpectedException]` — it was removed in NUnit 3+; use `Assert.That(…, Throws.TypeOf<T>())` instead.
- **Don't** share mutable state between tests via static fields — NUnit does not guarantee execution order across fixtures.
