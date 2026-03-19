---
description: "Use when writing, reviewing, or refactoring Jasmine tests, spies, or Jasmine-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Jasmine Guidelines

- **Do** use `jasmine.createSpy('name')` for standalone spies and `spyOn(object, 'method')` for method spies.
- **Do** chain spy strategies explicitly: `.and.returnValue(…)`, `.and.callFake(…)`, or `.and.throwError(…)`.
- **Do** use `jasmine.clock().install()` for timer-dependent tests — always call `jasmine.clock().uninstall()` in `afterEach`.
- **Do** use `expectAsync(promise).toBeResolvedTo(…)` or `expectAsync(promise).toBeRejectedWithError(…)` for async assertions.
- **Do** use custom matchers (`jasmine.addMatchers`) when a domain concept is asserted repeatedly — name them after the intent.
- **Don't** spy on `jasmine.clock()` timing functions (`setTimeout`, `setInterval`) — mock clock internals cannot be spied on.
- **Don't** spy on methods you don't assert against — unnecessary spies obscure the test's purpose.
