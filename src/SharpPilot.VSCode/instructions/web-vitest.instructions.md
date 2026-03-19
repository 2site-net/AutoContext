---
description: "Use when writing, reviewing, or refactoring Vitest tests, mocks, or Vitest-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Vitest Guidelines

- **Do** use `describe` / `it` (or `test`) blocks with clear, behaviour-focused names.
- **Do** use `expect(…).toBe(…)` for primitives and `expect(…).toEqual(…)` for deep object comparisons.
- **Do** use `vi.fn()` for spy/stub creation and `vi.mock('module')` for module-level mocking.
- **Do** use `vi.spyOn(object, 'method')` when you need to observe calls without replacing the implementation.
- **Do** use `beforeEach` / `afterEach` for per-test setup and teardown — keep `beforeAll` / `afterAll` for expensive shared resources.
- **Do** use inline snapshots (`toMatchInlineSnapshot()`) for small values; file snapshots for larger structures.
- **Do** use `vi.useFakeTimers()` and `vi.advanceTimersByTime()` when testing timer-dependent code — call `vi.useRealTimers()` in `afterEach`.
- **Do** co-locate test files next to source files or in a `__tests__` folder that mirrors the source structure.
- **Don't** use `vi.mock` inside `beforeEach` — hoist it to the top level of the file so Vitest can transform it at compile time.
- **Don't** assert on mock `calls` arrays directly — prefer `toHaveBeenCalledWith(…)` for clarity.
- **Don't** leave `.only` or `.skip` in committed tests — they silently reduce coverage.
