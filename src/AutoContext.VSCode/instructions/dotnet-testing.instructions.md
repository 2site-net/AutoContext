---
description: "Use when writing, reviewing, or refactoring .NET test naming, organization, or test-project conventions."
applyTo: "**/*Tests*.{cs,fs,vb,razor}"
version: "1.0.0"
---
# .NET Testing Conventions

## Naming & Organization

- [INST0001] **Do** mirror production namespaces — one test class per feature, suffixed `Tests`; prefix methods with `Should_` / `Should_not_` (e.g., `Should_do_something`, `Should_not_do_something`).
- [INST0002] **Do** suffix test doubles with `Fake` and store them in a `Fakes` folder.
- [INST0003] **Do** name integration tests after the most dependent type (e.g., say `VirtualCodeEditor` depends on `SyntaxHighlighter` then `VirtualCodeEditorTests`).
- [INST0004] **Don't** mix UI tests (e.g., Selenium, Playwright) into unit test projects — keep them in separate test projects.
- [INST0005] **Don't** give tests arbitrary names (e.g., `DebugSomeType`, `AnotherTypeEssentialTests`); always name them `<UnitUnderTest>Tests` (e.g., `SyntaxHighlighterTests`, `VirtualCodeEditorTests`).
- [INST0006] **Don't** add a new test class when an existing one already targets the same unit — extend it instead.

## .NET-Specific Practices

- [INST0007] **Do** distinguish dead code (never called) from test-only code — verify test utilities actually serve a clear purpose before removing them.
