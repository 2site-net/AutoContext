---
name: "design-principles (v1.0.0)"
description: "Apply when designing software systems (SOLID, dependency injection, composition, separation of concerns, error handling, logging)."
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
- [INST0009] **Don't** widen a production API's visibility, add members, or change a member's signature just to make it reachable from tests (e.g., promoting `private` → `internal`/`protected` so tests can call it, adding test-only seams, opening setters). If a unit feels untestable through its public API, that's a design signal — refactor the dependency it's coupled to behind an interface, extract the hard-to-reach logic into its own publicly testable type, or inject collaborators via the constructor. The test pressure exists to drive the design change, not to be relieved by widening visibility.
- [INST0010] **Do** keep constants scoped to the type that owns the concept. A constant used by only one type should stay private to that type. A constant shared across multiple consumers should have a clear public home, either on the owning type's API or in a dedicated domain-specific constants holder, e.g., `Limits` or `WellKnownPaths`; don't scatter `public` constants across unrelated classes.
