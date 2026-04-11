---
description: "Use when writing, reviewing, or refactoring JavaScript/TypeScript tests, test structure, or test organization."
applyTo: "**/*.{test,spec,cy}.{js,jsx,ts,tsx,mjs,mts}"
---
# Web Testing Strategy

- [INST0001] **Do** nest `describe` blocks — outer `describe` per class or module, inner `describe` per method or behavior.
- [INST0002] **Do** prefix test names with `should` or `should not` (e.g. `"should throw when value is null"`, `"should not throw when value is valid"`).
- [INST0003] **Do** use `beforeEach` / `afterEach` for per-test setup and teardown — reserve suite-level hooks for expensive shared resources.
- [INST0004] **Do** co-locate test files next to source files or in a `__tests__` / `test/` folder that mirrors the source structure.
- [INST0005] **Do** use `expect(…).toBe(…)` for primitives or identity checks and `expect(…).toEqual(…)` for deep object comparisons.
- [INST0006] **Do** prefer `toHaveBeenCalledWith(…)` over inspecting raw mock `calls` arrays.
- [INST0007] **Do** prefer `async/await` over `done` callbacks for async tests — callback-style is less readable and error-prone on timeout.
- [INST0008] **Don't** leave focus or skip markers (`.only`, `.skip`, `fdescribe`, `fit`, `xit`) in committed tests — they silently reduce coverage.
- [INST0009] **Do** use optional chaining (`?.`, `[i]?.`) instead of non-null assertions (`!.`) when accessing values after a soft/grouped guard assertion — a soft `toBeDefined()` or `toHaveLength()` that fails still continues execution, so subsequent property access must not throw a `TypeError`.
