---
name: "design-principles (v1.0.0)"
description: "Use when designing software systems: SOLID principles, dependency injection, composition, separation of concerns, error handling, and structured logging."
---

# Design Principles Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

- [INST0001] **Do** give descriptive names that reveal intent — prefer `retriesRemaining` over `n`, `calculateMonthlyTotal` over `calc`, `getUserById` over `getUser`, and `setContext` over `setCtx`; single-letter names are acceptable only as loop counters or well-known math symbols.
- [INST0002] **Do** use Separation of Concerns – keep data, domain and UI in distinct layers; keep UI types out of core code.
- [INST0003] **Do** use Dependency Injection – prefer constructor injection; avoid service locators and singletons.
- [INST0004] **Do** favor Composition over inheritance; expose clear extension points.
- [INST0005] **Do** follow SOLID (SRP · OCP · LSP · ISP · DIP).
- [INST0006] **Do** use Exception‑based Error Handling – wrap expected faults in `try/catch`; log unexpected ones.
- [INST0007] **Do** design for Testability First – isolate components and minimise mocks.
- [INST0008] **Do** use Structured Logging when available.
