---
description: "Use when writing, reviewing, or refactoring Jest tests, mocks, or Jest-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Jest Guidelines

- **Do** use `describe` / `it` (or `test`) blocks with clear, behaviour-focused names.
- **Do** use `expect(…).toBe(…)` for primitives and `expect(…).toEqual(…)` for deep object comparisons.
- **Do** use `jest.fn()` for spy/stub creation and `jest.mock('module')` for module-level mocking.
- **Do** use `jest.spyOn(object, 'method')` when you need to observe calls without replacing the implementation.
- **Do** use `beforeEach` / `afterEach` for per-test setup and teardown — keep `beforeAll` / `afterAll` for expensive shared resources.
- **Do** use inline snapshots (`toMatchInlineSnapshot()`) for small values; file snapshots for larger structures — update snapshots deliberately with `--updateSnapshot`, not blindly.
- **Do** use `jest.useFakeTimers()` and `jest.advanceTimersByTime()` when testing timer-dependent code — call `jest.useRealTimers()` in `afterEach`.
- **Do** co-locate test files next to source files or in a `__tests__` folder that mirrors the source structure.
- **Don't** use `jest.mock` inside `beforeEach` — hoist it to the top level of the file so Jest can transform it.
- **Don't** assert on mock `calls` arrays directly — prefer `toHaveBeenCalledWith(…)` for clarity.
- **Don't** leave `.only` or `.skip` in committed tests — they silently reduce coverage.
