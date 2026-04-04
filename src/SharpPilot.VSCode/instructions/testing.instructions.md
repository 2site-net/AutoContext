---
description: "Use when writing, reviewing, or refactoring any tests — regardless of language or framework."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts},**/*Tests*.{cs,fs,vb,razor}"
---
# Testing Strategy

## Workflow & Design

- [INST0001] **Do** follow TDD (red-green-refactor) — write the failing test first whether adding or refactoring.
- [INST0002] **Do** keep tests fast, reliable, isolated; focus on behavior ("what") not implementation ("how").
- [INST0003] **Do** assume a failing test means a production bug; fix code first, add a new test if the spec changed.
- [INST0004] **Do** validate that all tests pass before considering work complete.
- [INST0005] **Don't** add test-only code to production; keep helpers inside test projects or inject them via patterns (e.g., decorator).

## Structure & Assertions

- [INST0006] **Do** structure every test in AAA (Arrange-Act-Assert) style; separate the three sections with a blank line. In .NET tests `// Arrange` / `// Act` / `// Assert` comments are also acceptable.
- [INST0007] **Do** keep each test laser-focused: one behavior, one primary assertion, minimal mocks.
- [INST0008] **Do** mock only when truly required — prefer real implementations or simple fakes over mocking frameworks.
- [INST0009] **Do** break down large tests into smaller, focused ones; avoid monolithic tests.
- [INST0010] **Do** wrap test-specific helper logic in local functions to keep the test body focused and readable.
- [INST0011] **Do** pair positive and negative test cases for each behavior — test both the happy path and the boundary.
- [INST0012] **Don't** write tests that stray beyond the unit's scope; avoid broad tests that mix unrelated behaviors.
- [INST0013] **Don't** add comments inside tests — except for AAA markers in .NET. Rely on descriptive names to convey intent.
