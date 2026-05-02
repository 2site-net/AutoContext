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

- [INST0001] **Do** match the test namespace to the standard .NET convention — `<RootNamespace>` of the test project (or, when `<RootNamespace>` is not set in the `.csproj`, the project filename without extension) plus the folder path to the file, joined with dots (e.g. a file at `Pipes/PipeListenerTests.cs` in a project whose `<RootNamespace>` is `MyApp.Tests` belongs in namespace `MyApp.Tests.Pipes`).
- [INST0002] **Do** suffix every test class with `Tests` and prefix every test method with `Should_` or `Should_not_` (e.g., `Should_do_something`, `Should_not_do_something`).
- [INST0003] **Do** name integration tests after the most dependent type (e.g., say `VirtualCodeEditor` depends on `SyntaxHighlighter` then `VirtualCodeEditorTests`).
- [INST0004] **Don't** mix UI tests (e.g., Selenium, Playwright) into unit test projects — keep them in separate test projects.
- [INST0005] **Don't** give tests arbitrary names (e.g., `DebugSomeType`, `AnotherTypeEssentialTests`); always name them `<UnitUnderTest>Tests` (e.g., `SyntaxHighlighterTests`, `VirtualCodeEditorTests`).
- [INST0006] **Don't** add a new test class when an existing one already targets the same unit — extend it instead.

### .NET-Specific Practices

- [INST0007] **Do** distinguish dead code (never called) from test-only code — verify test utilities actually serve a clear purpose before removing them.
- [INST0008] **Don't** add XML doc comments (`/// <summary>`) to test classes or test methods — rely on descriptive names to convey intent.
