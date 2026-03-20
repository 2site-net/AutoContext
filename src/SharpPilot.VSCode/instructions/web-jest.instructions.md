---
description: "Use when writing, reviewing, or refactoring Jest tests, mocks, or Jest-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Jest Guidelines

- **Do** use `test` over `it` for test blocks — but if the existing codebase already uses one consistently, follow the project's convention.
- **Do** use tagged template syntax for `test.each` with a human-readable label column for test name interpolation.
- **Do** use `jest.fn()` for spy/stub creation and `jest.mock('module')` for module-level mocking.
- **Do** use `jest.spyOn(object, 'method')` when you need to observe calls without replacing the implementation.
- **Do** use inline snapshots (`toMatchInlineSnapshot()`) for small values; file snapshots for larger structures — update snapshots deliberately with `--updateSnapshot`, not blindly.
- **Do** use `jest.useFakeTimers()` and `jest.advanceTimersByTime()` when testing timer-dependent code — call `jest.useRealTimers()` in `afterEach`.
- **Do** pass the error class to `.toThrow()` — not a string message.
- **Don't** use `jest.mock` inside `beforeEach` — hoist it to the top level of the file so Jest can transform it.
