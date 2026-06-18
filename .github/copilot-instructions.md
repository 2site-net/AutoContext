# AutoContext Repository Instructions

> **AutoContext repo, AutoContext extension** — this repository builds and ships the AutoContext VS Code extension itself. The instructions below govern work inside this workspace; they take precedence over any installed-extension copies of the same instruction files.

## Instruction Precedence

- **Do** treat the instruction files inside `src/AutoContext.Engine/Instructions/` as the **authoritative source**.
- **Do** prefer the in-repo files over any instructions injected by an installed AutoContext VS Code extension (e.g. `c:\Users\<user>\.vscode\extensions\2site-net.autocontext-*\instructions\`) — those are the versions being actively edited and shipped from this workspace.
- **Don't** follow installed-extension instructions when they disagree with the in-repo copies.

## Build & Test

- **Do** route all compilation, testing, linting, and packaging through the AutoContext build tooling — never bare `npx vitest`, `npx tsc`, `dotnet build`, `dotnet test`, etc. The tooling configures paths, aliases, manifests, npm-install gating, and compilation order that bare invocations miss.
- **Do** use the orchestrator `build.ps1` from the repository root as the canonical entry point; consult the **Build Commands** table below for the command for each task.
- **Do** run `.\build.ps1 -Help` for the targets and switches (`TS`, `DotNet`, `-Clean`, `-WhatIf`), and the `scripts/*.ps1` wrappers (e.g. `scripts/package.ps1 -Help`) for packaging/publishing/tagging options like `-Local`, `-RuntimeIdentifier`, `All`.
- **Don't** invoke `npx vitest`, `npx tsc`, `dotnet build`, `dotnet test`, or any other build/test tool directly — go through `build.ps1` or a `scripts/*.ps1` wrapper.

### Two-tier workflow

The build logic lives in `scripts/AutoContext.Build.psm1`; `build.ps1` is the orchestrator and the `scripts/*.ps1` files are granular wrappers over the same module functions. Use the right tier for the job:

- **Inner loop (fast, narrow):** use the granular `scripts/*.ps1` wrappers while iterating — e.g. `scripts/compile.ps1 DotNet` (compile only, no tests/format gate), `scripts/test.ps1 TS`, `scripts/format.ps1`, `scripts/clean.ps1`. They skip the composite phases so they return quickly. `scripts/compile.ps1` is **compile-only** and deliberately differs from `build.ps1` (which also runs the format gate and unit tests).
- **Gate (full, authoritative):** before declaring work done or proposing a commit, run the full composite `.\build.ps1` (both stacks: compile + .NET format gate + unit tests). A green `build.ps1` — not a green inner-loop wrapper — is the bar for "done".
- **Note:** `scripts/test.ps1` compiles the selected stack(s) before running the unit suites by default, so an inner-loop test run never tests stale output. Pass `-NoCompile` to skip the compile and run `dotnet test`/vitest against the existing build (e.g. immediately after `scripts/compile.ps1` or `build.ps1`). npm installs are hash-gated on `package-lock.json`, so repeated TypeScript compiles skip `npm install` when dependencies are unchanged.
- **Do** run `scripts/build.tests.ps1` after changing `build.ps1`, the build module, or any wrapper — it exercises every action/target/switch combination (including the wrappers) under `-WhatIf`.

### Build Commands

| Task                                            | Command                                |
|-------------------------------------------------|----------------------------------------|
| Compile everything + run unit tests (the gate)  | `.\build.ps1`                          |
| Compile TypeScript + TS tests                   | `.\build.ps1 TS`                       |
| Compile .NET + .NET tests                       | `.\build.ps1 DotNet`                   |
| Clean all build artifacts                       | `.\build.ps1 -Clean`                   |
| Clean then run the gate                         | `.\build.ps1 -Clean All`               |
| Inner-loop compile (compile only, no tests)     | `.\scripts\compile.ps1 [TS\|DotNet]`   |
| Inner-loop compile + tests                      | `.\scripts\test.ps1 [TS\|DotNet]`      |
| Inner-loop tests only (skip the compile)        | `.\scripts\test.ps1 -NoCompile`        |
| Smoke-test the extension / .NET                 | `.\scripts\test.ps1 -Smoke [DotNet]`   |
| Inner-loop .NET format gate                     | `.\scripts\format.ps1`                 |
| Prepare (clean + compile + test + copy assets)  | `.\scripts\prepare.ps1`               |
| Package                                         | `.\scripts\package.ps1`               |
| Publish                                         | `.\scripts\publish.ps1`               |
| Tag a release                                   | `.\scripts\tag.ps1 <version>`         |
| Run the build self-test suite                   | `.\scripts\build.tests.ps1`            |

> `build.ps1` is the compile + format + unit-test gate; it always runs unit
> tests. There is no standalone `Test` action — `build.ps1` tests always run
> with a fresh compile. For a tests-only run against an already-compiled tree,
> use `scripts/test.ps1 -NoCompile`. Packaging, publishing, tagging, and the
> staged `Prepare` layout live only in the `scripts/*.ps1` wrappers.

## MCP Tool Scope

Some AutoContext MCP tools are trained against production-code patterns and produce noisy or actively wrong findings when applied to test code (where the testing-specific instructions — `testing`, `dotnet-testing`, `dotnet-xunit`, `web-vitest`, etc. — apply instead).

- **Don't** invoke `analyze_csharp_code` on anything under `tests/**` — that subtree holds all C# test code, including dedicated `<Project>.Tests.Support` projects and `Support/` folders inside `<Project>.Tests` projects. Validate those against the matching testing instructions (`testing`, `dotnet-testing`, `dotnet-xunit`) instead.
- **Don't** invoke `analyze_typescript_code` on anything under `src/AutoContext.*/tests/**` — that subtree holds all TypeScript test code and test support. Validate those against the matching testing instructions (`testing`, `web-testing`, `web-vitest`, `web-mocha`, `web-playwright`) instead.
- **Don't** assume new production-code-shaped MCP analyzers shipped from this repo are safe to run on test code. Apply the same scope rule, and when in doubt, read the tool's `description` in `src/AutoContext.Mcp.Server/mcp-tools-registry.json` to confirm scope before invoking.

## Versioning

- **Don't** modify version numbers anywhere in the codebase without explicit user permission. This includes (but is not limited to) `version.json`, `package.json` `version` fields, `.csproj` `<Version>` / `<VersionPrefix>` properties, instruction-file frontmatter `name: "<id> (vX.Y.Z)"` strings, and any other semver string baked into source.
- **Do** treat version bumps as a deliberate, user-driven action performed via `versionize.ps1` — never opportunistically as part of an unrelated change.
