---
description: "Use when writing, reviewing, or refactoring JavaScript/TypeScript tests, test structure, or test organization."
applyTo: "**/*.{test,spec,cy}.{js,jsx,ts,tsx,mjs,mts}"
---
# Web Testing Strategy

- **Do** nest `describe` blocks — outer `describe` per class or module, inner `describe` per method or behavior.
- **Do** prefix test names with `should` or `should not` (e.g. `"should throw when value is null"`, `"should not throw when value is valid"`).
- **Do** pair positive and negative test cases for each behavior — test both the happy path and the boundary.
- **Do** structure tests in AAA (Arrange–Act–Assert) — separate sections with a blank line when the test is non-trivial.
- **Do** use `beforeEach` / `afterEach` for per-test setup and teardown — reserve suite-level hooks for expensive shared resources.
- **Do** co-locate test files next to source files or in a `__tests__` / `test/` folder that mirrors the source structure.
- **Do** use `expect(…).toBe(…)` for primitives or identity checks and `expect(…).toEqual(…)` for deep object comparisons.
- **Do** prefer `toHaveBeenCalledWith(…)` over inspecting raw mock `calls` arrays.
- **Do** prefer `async/await` over `done` callbacks for async tests — callback-style is less readable and error-prone on timeout.
- **Don't** leave focus or skip markers (`.only`, `.skip`, `fdescribe`, `fit`, `xit`) in committed tests — they silently reduce coverage.
