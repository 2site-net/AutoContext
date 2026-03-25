---
description: "Use when writing, reviewing, or refactoring JavaScript/TypeScript tests, test structure, or test organization."
applyTo: "**/*.{test,spec,cy}.{js,jsx,ts,tsx,mjs,mts}"
---
# Web Testing Strategy

- [INST0001] **Do** nest `describe` blocks — outer `describe` per class or module, inner `describe` per method or behavior.
- [INST0002] **Do** prefix test names with `should` or `should not` (e.g. `"should throw when value is null"`, `"should not throw when value is valid"`).
- [INST0003] **Do** pair positive and negative test cases for each behavior — test both the happy path and the boundary.
- [INST0004] **Do** structure tests in AAA (Arrange–Act–Assert) — separate sections with a blank line when the test is non-trivial.
- [INST0005] **Do** use `beforeEach` / `afterEach` for per-test setup and teardown — reserve suite-level hooks for expensive shared resources.
- [INST0006] **Do** co-locate test files next to source files or in a `__tests__` / `test/` folder that mirrors the source structure.
- [INST0007] **Do** use `expect(…).toBe(…)` for primitives or identity checks and `expect(…).toEqual(…)` for deep object comparisons.
- [INST0008] **Do** prefer `toHaveBeenCalledWith(…)` over inspecting raw mock `calls` arrays.
- [INST0009] **Do** prefer `async/await` over `done` callbacks for async tests — callback-style is less readable and error-prone on timeout.
- [INST0010] **Don't** leave focus or skip markers (`.only`, `.skip`, `fdescribe`, `fit`, `xit`) in committed tests — they silently reduce coverage.
