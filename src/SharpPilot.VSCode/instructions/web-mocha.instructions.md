---
description: "Use when writing, reviewing, or refactoring Mocha tests or Mocha-specific configuration."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Mocha Guidelines

- **Do** use `describe` / `it` blocks with clear, behaviour-focused names.
- **Do** pair Mocha with a dedicated assertion library (Chai, `node:assert`, or `node:assert/strict`) — Mocha provides no built-in assertions.
- **Do** return a Promise or use `async/await` for async tests — never mix callbacks (`done`) with Promises.
- **Do** use `beforeEach` / `afterEach` for per-test setup and teardown — keep `before` / `after` for expensive shared resources.
- **Do** use Sinon.js (`sinon.stub`, `sinon.spy`, `sinon.fake`) for mocking when needed — always call `sinon.restore()` in `afterEach`.
- **Do** set a reasonable `--timeout` globally (for integration tests) and prefer the default 2 000 ms for unit tests.
- **Do** use `--exit` flag in CI to force Mocha to exit after tests complete when lingering handles are unavoidable.
- **Do** co-locate test files next to source files or in a `test/` folder that mirrors the source structure.
- **Don't** use arrow functions (`() =>`) for `describe` / `it` / `before*` / `after*` when you need access to Mocha's `this` context (e.g., `this.timeout()`).
- **Don't** leave `.only` or `.skip` in committed tests — they silently reduce coverage.
- **Don't** use `done` callbacks when `async/await` is available — callback-style tests are harder to read and prone to silent timeouts.
