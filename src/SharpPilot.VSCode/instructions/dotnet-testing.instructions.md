---
description: "Use when writing, reviewing, or refactoring tests, test naming, TDD workflows, or test organization in .NET."
applyTo: "**/*Tests*.{cs,vb,razor}"
---
# Testing Strategy

- **Do** follow TDD (red–green–refactor)—write the failing test first whether adding or refactoring.
- **Do** keep tests fast, reliable, isolated; focus on behavior ("what") not implementation ("how").
- **Do** assume a failing test means a production bug; fix code first, add a new test if the spec changed.
- **Do** mirror production namespaces—one test class per feature, suffixed `Tests`; prefix methods with `Should_` / `Should_not_` (e.g., `Should_do_something`, `Should_not_do_something`).
- **Do** suffix test doubles with `Fake` and store them in a `Fakes` folder.
- **Do** keep each test laser-focused: one behavior, one primary assertion, minimal mocks.
- **Do** mock only when truly required — prefer real implementations or simple fakes over mocking frameworks.
- **Do** structure every test in AAA (Arrange–Act–Assert) style; separate the three sections with a blank line or `// Arrange` / `// Act` / `// Assert` comments.
- **Do** name integration tests after the most dependent type (e.g., say `VirtualCodeEditor` depends on `SyntaxHighlighter` then `VirtualCodeEditorTests`).
- **Do** break down large tests into smaller, focused ones; avoid monolithic tests.
- **Do** wrap test‑specific helper logic in local functions to keep the test body focused and readable.
- **Do** distinguish dead code (never called) from test-only code — verify test utilities actually serve a clear purpose before removing them.
- **Do** validate that all tests pass before considering work complete.
- **Don't** add test-only code to production; keep helpers inside test projects or inject them via patterns (e.g., decorator).
- **Don't** mix UI tests (e.g., Selenium, Playwright) into unit test projects — keep them in separate test projects.
- **Don't** give tests arbitrary names (e.g., `DebugSomeType`, `AnotherTypeEssentialTests`); always name them `<UnitUnderTest>Tests` (e.g., `SyntaxHighlighterTests`, `VirtualCodeEditorTests`).
- **Don't** add a new test class when an existing one already targets the same unit—extend it instead.
- **Don't** write tests that stray beyond the unit's scope; avoid broad tests that mix unrelated behaviors.
- **Don't** add comments, regions, or XML docs to tests—except for AAA (Arrange‑Act‑Assert) markers. Rely on descriptive names to convey intent to tell the story.
