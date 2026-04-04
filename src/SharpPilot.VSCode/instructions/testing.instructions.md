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
- [INST0007] **Do** keep each test laser-focused: one behavior, minimal mocks. Multiple assertions are fine when they verify facets of the same outcome — group them with the framework's API (see INST0008).
- [INST0008] **Do** use the framework's grouped-assertion API when a test has multiple assertions — e.g., `Assert.Multiple()` in xUnit, `expect.soft()` in Vitest — so a first failure doesn't hide the rest.
- [INST0009] **Do** use the test framework's lifecycle hooks for shared initialization and cleanup — don't inline repetitive setup or cleanup in individual tests.
- [INST0010] **Do** mock only when truly required — prefer real implementations or simple fakes over mocking frameworks.
- [INST0011] **Do** break down large tests into smaller, focused ones; avoid monolithic tests.
- [INST0012] **Do** wrap test-specific helper logic in local functions to keep the test body focused and readable.
- [INST0013] **Do** group similar statements (variable bindings, function calls, assertions) together and separate each group with a blank line for readability.
- [INST0014] **Do** pair positive and negative test cases for each behavior — test both the happy path and the boundary.
- [INST0015] **Don't** write tests that stray beyond the unit's scope; avoid broad tests that mix unrelated behaviors.
- [INST0016] **Don't** add comments inside tests — except for AAA markers in .NET. Rely on descriptive names to convey intent.
- [INST0017] **Don't** use control structures (`for`, `while`, `if`, `switch`, `try/catch`) inside test bodies — use parameterized tests (`test.each`, `[Theory]`) for iteration, separate tests for branches, and assertion APIs (e.g., `.toThrow()`) for expected exceptions.
- [INST0018] **Don't** place assertions before or between act phases — arrange first, act once, then assert; split multi-step scenarios into separate tests.
