---
description: "Use when debugging issues, troubleshooting failures, reproducing bugs, or writing regression tests in C#."
applyTo: "**/*.cs"
---
# Debugging & Troubleshooting

- **Do** reproduce bugs in isolation—create a minimal repro case or failing test before diving in.
- **Do** write a failing unit or integration test for any bug you fix; that test then becomes part of your regression suite.
- **Do** log at appropriate levels (Debug / Verbose) and include correlation IDs to trace multi-component flows.
- **Don't** leave temporary debug helpers in committed code (e.g., `Debugger.Break()`, commented-out code).
- **Don't** guess at fixes—verify the hypothesis by reading the relevant code and tracing the logic.
