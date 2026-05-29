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
- [INST0009] **Do** keep constants scoped to the type that owns the concept. A constant used by only one type should stay private to that type. A constant shared across multiple consumers should have a clear public home, either on the owning type's API or in a dedicated domain-specific constants holder, e.g., `Limits` or `WellKnownPaths`; don't scatter `public` constants across unrelated classes.
- [INST0010] **Do** make comments answer **what** (API documentation: what a type or member is and how callers use it) or **why** (inline comments: why the code is shaped this way — design trade-offs, surprising algorithms, non-obvious constraints). The code itself answers **how**; don't paraphrase it in prose. XML docs / JSDoc / doc-comments that walk through "this method iterates over X, calls Y, then returns Z" add noise instead of context — a reader who needs that detail reads the body. If an algorithm genuinely benefits from prose, explain *why* the approach was chosen, not the mechanical steps.
- [INST0011] **Don't** widen a production API's visibility, add members, or change a member's signature just to make it reachable from tests (e.g., promoting `private` → `internal`/`protected` so tests can call it, adding test-only seams, opening setters). If a unit feels untestable through its public API, that's a design signal — refactor the dependency it's coupled to behind an interface, extract the hard-to-reach logic into its own publicly testable type, or inject collaborators via the constructor. The test pressure exists to drive the design change, not to be relieved by widening visibility.
- [INST0012] **Don't** reference transient process artifacts in code comments — implementation-plan filenames, phase or step markers (`P5`, `Phase 3`, `Step 2`), "see `docs/foo-plan.md`", or similar pointers to planning documents. Plans and phases are ephemeral; the code outlives them. If the rationale is durable, restate it in the comment in its own words; if it isn't, the comment doesn't belong in the code. Durable ticket markers a developer explicitly chooses to leave behind — `// TODO(#123)`, `// FIX(#123)`, `// REVIEW(#123)` linking to long-lived issue trackers — are fine, but don't add them on your own initiative; only insert them when the developer explicitly asks for them or writes them themselves.
