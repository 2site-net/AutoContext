---
name: "testing (v1.0.0)"
description: "Apply when writing or reviewing tests, regardless of language or framework."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts},**/*Tests*.{cs,fs,vb,razor}"
---

# Testing Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Workflow & Design

- [INST0001] **Do** follow TDD (red-green-refactor) — write the failing test first whether adding or refactoring.
- [INST0002] **Do** keep tests fast, reliable, isolated; focus on behavior ("what") not implementation ("how").
- [INST0003] **Do** assume a failing test means a production bug; fix code first, add a new test if the spec changed.
- [INST0004] **Do** validate that all tests pass before considering work complete.
- [INST0005] **Don't** add test-only code to production; keep helpers inside test projects or inject them via patterns (e.g., decorator).

### Structure & Assertions

- [INST0006] **Do** structure every test in AAA (Arrange-Act-Assert) style; place a single blank line between setup, action, and assertion groups. Consecutive bindings and consecutive calls within a group stay together — don't insert blank lines inside a group. In .NET tests `// Arrange` / `// Act` / `// Assert` comments are also acceptable.
- [INST0007] **Do** keep each test laser-focused: one behavior, minimal mocks. Multiple assertions are fine when they verify facets of the same outcome — group them with the framework's API (see [#INST0008]).
- [INST0008] **Do** use the framework's grouped-assertion API when a test has multiple assertions — e.g., `Assert.Multiple()` in xUnit, `expect.soft()` in Vitest — so a first failure doesn't hide the rest.
- [INST0009] **Do** use the test framework's lifecycle hooks for shared initialization and cleanup — don't inline repetitive setup or cleanup in individual tests.
- [INST0010] **Do** mock only when truly required — prefer real implementations or simple fakes over mocking frameworks.
- [INST0011] **Do** break down large tests into smaller, focused ones; avoid monolithic tests.
- [INST0012] **Do** wrap test-specific helper logic in local functions to keep the test body focused and readable.
- [INST0013] **Do** pair positive and negative test cases for each behavior — test both the happy path and the boundary.
- [INST0014] **Do** place reusable test-support code (fakes, fixtures, fake data, helpers, and utilities) in the platform’s conventional test-support root, mirroring the production project’s organization. If production code is organized by feature, domain, layer, or concern, organize test-support code the same way; if production code is flat, keep test-support flat too. For concrete paths, casing, and layout conventions, see the platform-specific testing instructions, e.g., `dotnet-testing.instructions.md` or `web-testing.instructions.md`.
- [INST0015] **Do** put genuinely cross-cutting test-support helpers that don't belong to one production area in a flat `Shared/` subfolder under the support root, e.g., `Support/Shared/` in .NET or `tests/support/shared/` / `src/shared/` in TypeScript. The `Shared/` subfolder applies only where the support root also holds production-mirroring helpers beside it (e.g., a single test project's `Support/` folder); a project whose entire purpose is sharing has no use for it, because everything in it is already shared. Don't add `Shared/` when production is flat and there's no higher-level structure to share across.
- [INST0016] **Do** follow these naming conventions for test-support types:
  - Fakes: `Fake<TypeName>` (e.g., `FakeFileSystem`).
  - Fixtures: `<TypeName>Fixture` (e.g., `DatabaseFixture`).
  - Fake data: `<TypeName>FakeData` (e.g., `CustomerFakeData`).
  - Test utilities and helpers: use `<TypeName>Test<RoleName>` when the final noun would otherwise read like a production-code responsibility, especially when omitting `Test` would introduce a naming conflict or make the type easy to confuse with production code. Examples: `HttpTestClient`, `JsonTestExtensions`, `UserRepositoryTestFactory`, `AuthenticationTestHandler`.
  - Omit the `Test` infix only when the final noun already has clear test-support meaning in the current codebase and does not conflict with, or read like, production code.
- [INST0017] **Do** reach for a fixture (not a factory) whenever the test-support type needs to hold state across uses or perform cleanup/teardown. Factories are for stateless construction of fresh instances; once disposal, lifetime, or shared state enters the picture, it's a fixture.
- [INST0018] **Don't** organize test-support code by artifact kind, e.g., `Fakes/`, `Fixtures/`, `Utils/`, `Helpers/`. Use those folder names only when they are part of the mirrored production feature/domain structure.
- [INST0019] **Don't** write tests that stray beyond the unit's scope; avoid broad tests that mix unrelated behaviors.
- [INST0020] **Don't** test private or internal methods directly (via `as any`, reflection, `[InternalsVisibleTo]`, etc.) — test the behavior through the public API. If a private method is complex enough to feel like it needs its own tests, consider whether it should be extracted into a separate, publicly testable unit.
- [INST0021] **Don't** add comments inside tests — except for AAA markers in .NET. Rely on descriptive names to convey intent.
- [INST0022] **Don't** use control structures (`for`, `while`, `if`, `switch`, `try/catch`) inside test bodies — use parameterized tests (`test.each`, `[Theory]`) for iteration, separate tests for branches, and assertion APIs (e.g., `.toThrow()`) for expected exceptions.
- [INST0023] **Don't** interleave assertions into the Arrange or Act sections — arrange first, act once, then put every assertion in the Assert section; split multi-step scenarios into separate tests. The sole exception is pre-Arrange skip guards (e.g., xUnit's `Assert.Skip(...)`), which short-circuit the test before any Arrange runs and may sit above the Arrange block.
