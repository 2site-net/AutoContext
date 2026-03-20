---
description: "Use when writing, reviewing, or refactoring Vitest tests, mocks, or Vitest-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Vitest Guidelines

- **Do** use `test` over `it` for test blocks — but if the existing codebase already uses one consistently, follow the project's convention.
- **Do** use tagged template syntax for `test.each` with a human-readable label column for test name interpolation.
- **Do** use `vi.fn()` for spy/stub creation and `vi.mock('module')` for module-level mocking.
- **Do** use `vi.spyOn(object, 'method')` when you need to observe calls without replacing the implementation.
- **Do** use inline snapshots (`toMatchInlineSnapshot()`) for small values; file snapshots for larger structures.
- **Do** use `vi.useFakeTimers()` and `vi.advanceTimersByTime()` when testing timer-dependent code — call `vi.useRealTimers()` in `afterEach`.
- **Do** pass the error class to `.toThrow()` — not a string message.
- **Don't** use `vi.mock` inside `beforeEach` — hoist it to the top level of the file so Vitest can transform it at compile time.
