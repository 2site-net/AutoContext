# Build & Test

All compilation, testing, linting, and packaging MUST go through the
project's build infrastructure. Never invoke tools directly.

| Task | Command |
|---|---|
| Compile everything | `.\build.ps1 Compile` |
| Compile TypeScript only | `.\build.ps1 Compile TS` |
| Compile .NET only | `.\build.ps1 Compile DotNet` |
| Unit-test everything | `.\build.ps1 Test` |
| Unit-test TypeScript only | `.\build.ps1 Test TS` |
| Unit-test .NET only | `.\build.ps1 Test DotNet` |
| Smoke-test the VS Code extension | `.\build.ps1 Test -Smoke` |
| Prepare (clean + compile + test + copy assets) | `.\build.ps1 Prepare` |
| Package | `.\build.ps1 Package` |

Do **not** run `npx vitest`, `npx tsc`, `dotnet build`, `dotnet test`,
or any other build/test tool directly — the build script configures
paths, aliases, manifests, and compilation order that bare tool
invocations will miss.

All `build.ps1` commands run from the repository root.

Run `.\build.ps1 -Help` for the full list of actions, targets, and
switches (e.g. `-Clean`, `-Local`, `-WhatIf`, `-RuntimeIdentifier`).
