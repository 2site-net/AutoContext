# Contributing

Contributions are welcome. Before submitting, please review the following.

## Contributor License Agreement

By submitting a contribution, you agree to the [Contributor License Agreement](CLA.md).

## Getting started

1. Fork the repository and clone your fork.
2. Install prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/) 18+.
3. Run `pwsh ./build.ps1` to compile and test everything.

## Development workflow

- Create a feature branch from `main`.
- Keep commits small and focused. Follow [Conventional Commits](https://www.conventionalcommits.org/).
- Run `pwsh ./build.ps1` before pushing — all tests must pass.
- Open a pull request against `main`.

## What we're looking for

- Bug fixes with a clear description of the problem
- New instruction files for technologies not yet covered
- Improvements to existing MCP tool checkers
- Documentation fixes

## Before large changes

Please open an issue before submitting large refactors or features that materially change the scope of the project.

## Code style

The project follows its own coding standards via SharpPilot's instruction files. See `src/SharpPilot.VSCode/instructions/` for the full set of guidelines.
