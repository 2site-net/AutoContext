---
name: "dotnet-testing (v1.0.0)"
description: "Apply when writing or reviewing .NET test naming, organization, or test-project conventions."
applyTo: "**/*Tests*.{cs,fs,vb,razor}"
---

# .NET Testing Instructions

## MCP Tool Validation

After editing or generating any C# source file, call the
`analyze_csharp_code` MCP tool on the changed source. Pass the file
contents as `content` and the file's absolute path as `originalPath`.
For test files, also pass the production type's namespace as
`originalNamespace` and the test file path as `comparedPath`. Treat
any reported violation as blocking — fix it before reporting the work
as done.

## Rules

### Naming & Organization

- [INST0001] **Do** mirror production namespaces — one test class per feature, suffixed `Tests`; prefix methods with `Should_` / `Should_not_` (e.g., `Should_do_something`, `Should_not_do_something`).
- [INST0002] **Do** name integration tests after the most dependent type (e.g., say `VirtualCodeEditor` depends on `SyntaxHighlighter` then `VirtualCodeEditorTests`).
- [INST0003] **Don't** mix UI tests (e.g., Selenium, Playwright) into unit test projects — keep them in separate test projects.
- [INST0004] **Don't** give tests arbitrary names (e.g., `DebugSomeType`, `AnotherTypeEssentialTests`); always name them `<UnitUnderTest>Tests` (e.g., `SyntaxHighlighterTests`, `VirtualCodeEditorTests`).
- [INST0005] **Don't** add a new test class when an existing one already targets the same unit — extend it instead.

### .NET-Specific Practices

- [INST0006] **Do** distinguish dead code (never called) from test-only code — verify test utilities actually serve a clear purpose before removing them.
