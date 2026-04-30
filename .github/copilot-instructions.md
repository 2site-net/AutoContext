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

| Task                                            | Command                       |
|-------------------------------------------------|-------------------------------|
| Compile everything                              | `.\build.ps1 Compile`         |
| Compile TypeScript only                         | `.\build.ps1 Compile TS`      |
| Compile .NET only                               | `.\build.ps1 Compile DotNet`  |
| Unit-test everything                            | `.\build.ps1 Test`            |
| Unit-test TypeScript only                       | `.\build.ps1 Test TS`         |
| Unit-test .NET only                             | `.\build.ps1 Test DotNet`     |
| Smoke-test the VS Code extension                | `.\build.ps1 Test -Smoke`     |
| Prepare (clean + compile + test + copy assets)  | `.\build.ps1 Prepare`         |
| Package                                         | `.\build.ps1 Package`         |

## Versioning

- **Don't** modify version numbers anywhere in the codebase without explicit user permission. This includes (but is not limited to) `version.json`, `package.json` `version` fields, `.csproj` `<Version>` / `<VersionPrefix>` properties, instruction-file frontmatter `name: "<id> (vX.Y.Z)"` strings, and any other semver string baked into source.
- **Do** treat version bumps as a deliberate, user-driven action performed via `versionize.ps1` — never opportunistically as part of an unrelated change.
