---
description: "Use when writing, reviewing, or refactoring Jasmine tests, spies, or Jasmine-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Jasmine Guidelines

- **Do** use `describe` / `it` blocks with clear, behaviour-focused names.
- **Do** use `expect(…).toBe(…)` for identity checks and `expect(…).toEqual(…)` for deep equality.
- **Do** use `jasmine.createSpy('name')` for standalone spies and `spyOn(object, 'method')` for method spies.
- **Do** chain spy strategies explicitly: `.and.returnValue(…)`, `.and.callFake(…)`, or `.and.throwError(…)`.
- **Do** use `beforeEach` / `afterEach` for per-test setup and teardown — keep `beforeAll` / `afterAll` for expensive shared resources.
- **Do** use `jasmine.clock().install()` for timer-dependent tests — always call `jasmine.clock().uninstall()` in `afterEach`.
- **Don't** spy on `jasmine.clock()` timing functions (`setTimeout`, `setInterval`) — mock clock internals cannot be spied on.
- **Do** use `expectAsync(promise).toBeResolvedTo(…)` or `expectAsync(promise).toBeRejectedWithError(…)` for async assertions.
- **Do** use custom matchers (`jasmine.addMatchers`) when a domain concept is asserted repeatedly — name them after the intent.
- **Don't** use `done` callbacks for async tests when `async/await` is available — it's less readable and error-prone on timeout.
- **Don't** leave `fdescribe` / `fit` / `xit` / `xdescribe` in committed tests — they silently skip or focus tests.
- **Don't** spy on methods you don't assert against — unnecessary spies obscure the test's purpose.
