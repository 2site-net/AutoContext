# AutoContext Repository Instructions

> **AutoContext repo, AutoContext extension** — this repository builds and ships the AutoContext VS Code extension itself. The instructions below govern work inside this workspace; they take precedence over any installed-extension copies of the same instruction files.

## Instruction Precedence

- **Do** treat the instruction files inside `src/AutoContext.VsCode/instructions/` as the **authoritative source**.
- **Do** prefer the in-repo files over any instructions injected by an installed AutoContext VS Code extension (e.g. `c:\Users\<user>\.vscode\extensions\2site-net.autocontext-*\instructions\`) — those are the versions being actively edited and shipped from this workspace.
- **Don't** follow installed-extension instructions when they disagree with the in-repo copies.

## Build & Test

- **Do** route all compilation, testing, linting, and packaging through `build.ps1` from the repository root — it configures paths, aliases, manifests, and compilation order that bare tool invocations miss.
- **Do** consult the **Build Commands** table below for the canonical command for each task.
- **Do** run `.\build.ps1 -Help` for the full list of actions, targets, and switches (e.g. `-Clean`, `-Local`, `-WhatIf`, `-RuntimeIdentifier`).
- **Don't** invoke `npx vitest`, `npx tsc`, `dotnet build`, `dotnet test`, or any other build/test tool directly.

### Build Commands

| Task                                            | Command                                |
|-------------------------------------------------|----------------------------------------|
| Compile everything + run unit tests             | `.\build.ps1 Compile`                  |
| Compile TypeScript + TS tests                   | `.\build.ps1 Compile TS`               |
| Compile .NET + .NET tests                       | `.\build.ps1 Compile DotNet`           |
| Compile only — skip unit tests                  | `.\build.ps1 Compile -NoTest`          |
| Compile TypeScript only — skip tests            | `.\build.ps1 Compile TS -NoTest`       |
| Compile .NET only — skip tests                  | `.\build.ps1 Compile DotNet -NoTest`   |
| Smoke-test the VS Code extension (full pipeline) | `.\build.ps1 Compile -Smoke`           |
| Smoke-test .NET only                            | `.\build.ps1 Compile -Smoke DotNet`    |
| Prepare (clean + compile + test + copy assets)  | `.\build.ps1 Prepare`                  |
| Package                                         | `.\build.ps1 Package`                  |

> `Compile` always runs unit tests unless you pass `-NoTest`. There is no
> standalone `Test` action — tests always run with a fresh compile.

## MCP Tool Scope

Some AutoContext MCP tools are trained against production-code patterns and produce noisy or actively wrong findings when applied to test code (where the testing-specific instructions — `testing`, `dotnet-testing`, `dotnet-xunit`, `web-vitest`, etc. — apply instead).

- **Do** restrict `analyze_csharp_code` to production C# files — anything *not* matching `**/*Tests*.{cs,razor}` and not under `**/tests/**` or `**/Support/**`.
- **Do** restrict `analyze_typescript_code` to production TypeScript/JavaScript files — anything *not* matching `**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}` and not under `**/tests/**`, `**/__tests__/**`, `**/__mocks__/**`, `**/support/**`, or `**/Support/**`.
- **Do** apply the same scope rule to any future production-code-shaped MCP analyzer that ships from this repo (e.g. additional language analyzers). When in doubt, read the tool's `description` in `src/AutoContext.Mcp.Server/mcp-tools-registry.json` to confirm scope before invoking.
- **Don't** invoke `analyze_csharp_code` on test files, test fixtures, fakes, harness code, or shared testing projects (e.g. `AutoContext.Framework.Tests.Support`) — validate those against the matching testing instructions instead.
- **Don't** invoke `analyze_typescript_code` on test files, spec files, mocks, fixtures, or harness code — validate those against the matching testing instructions (`testing`, `web-testing`, `web-vitest`, `web-mocha`, `web-playwright`) instead.

## Versioning

- **Don't** modify version numbers anywhere in the codebase without explicit user permission. This includes (but is not limited to) `version.json`, `package.json` `version` fields, `.csproj` `<Version>` / `<VersionPrefix>` properties, instruction-file frontmatter `name: "<id> (vX.Y.Z)"` strings, and any other semver string baked into source.
- **Do** treat version bumps as a deliberate, user-driven action performed via `versionize.ps1` — never opportunistically as part of an unrelated change.
