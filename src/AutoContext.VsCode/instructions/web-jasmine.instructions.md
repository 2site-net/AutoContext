---
name: "web-jasmine (v1.0.0)"
description: "Use when writing, reviewing, or refactoring Jasmine tests, spies, or Jasmine-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Jasmine Guidelines

- [INST0001] **Do** use `jasmine.createSpy('name')` for standalone spies and `spyOn(object, 'method')` for method spies.
- [INST0002] **Do** chain spy strategies explicitly: `.and.returnValue(…)`, `.and.callFake(…)`, or `.and.throwError(…)`.
- [INST0003] **Do** use `jasmine.clock().install()` for timer-dependent tests — always call `jasmine.clock().uninstall()` in `afterEach`.
- [INST0004] **Do** use `expectAsync(promise).toBeResolvedTo(…)` or `expectAsync(promise).toBeRejectedWithError(…)` for async assertions.
- [INST0005] **Do** use custom matchers (`jasmine.addMatchers`) when a domain concept is asserted repeatedly — name them after the intent.
- [INST0006] **Don't** spy on `jasmine.clock()` timing functions (`setTimeout`, `setInterval`) — mock clock internals cannot be spied on.
- [INST0007] **Don't** spy on methods you don't assert against — unnecessary spies obscure the test's purpose.
