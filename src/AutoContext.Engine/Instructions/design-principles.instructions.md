---
name: "design-principles (v1.0.0)"
description: "Apply when designing software systems (SOLID, dependency injection, composition, separation of concerns, error handling, logging)."
---

# Design Principles Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Architecture

- [INST0001] **Do** use Separation of Concerns – keep data, domain and UI in distinct layers; keep UI types out of core code.
- [INST0002] **Do** use Dependency Injection – prefer constructor injection; avoid service locators and singletons.
- [INST0003] **Do** favor Composition over inheritance; expose clear extension points.
- [INST0004] **Do** follow SOLID (SRP · OCP · LSP · ISP · DIP).
- [INST0005] **Do** design for Testability First – isolate components and minimise mocks.

### Code Quality

- [INST0006] **Do** give descriptive names that reveal intent — prefer `retriesRemaining` over `n`, `calculateMonthlyTotal` over `calc`, `getUserById` over `getUser`, and `setContext` over `setCtx`; single-letter names are acceptable only as loop counters or well-known math symbols.
- [INST0007] **Do** keep constants scoped to the type that owns the concept. A constant used by only one type should stay private to that type. A constant shared across multiple consumers should have a clear public home, either on the owning type's API or in a dedicated domain-specific constants holder, e.g., `Limits` or `WellKnownPaths`; don't scatter `public` constants across unrelated classes.
- [INST0008] **Do** make comments answer **what** (API documentation: what a type or member is and how callers use it) or **why** (inline comments: why the code is shaped this way — design trade-offs, surprising algorithms, non-obvious constraints). The code itself answers **how**; don't paraphrase it in prose. XML docs / JSDoc / doc-comments that walk through "this method iterates over X, calls Y, then returns Z" add noise instead of context — a reader who needs that detail reads the body. If an algorithm genuinely benefits from prose, explain *why* the approach was chosen, not the mechanical steps.
- [INST0009] **Don't** reference transient process artifacts in code comments — implementation-plan filenames, phase or step markers (`P5`, `Phase 3`, `Step 2`), "see `docs/foo-plan.md`", or similar pointers to planning documents. Plans and phases are ephemeral; the code outlives them. If the rationale is durable, restate it in the comment in its own words; if it isn't, the comment doesn't belong in the code. Durable ticket markers a developer explicitly chooses to leave behind — `// TODO(#123)`, `// FIX(#123)`, `// REVIEW(#123)` linking to long-lived issue trackers — are fine, but don't add them on your own initiative; only insert them when the developer explicitly asks for them or writes them themselves.

### API Surface

- [INST0010] **Do** validate every API boundary on entry — reject inputs that violate the contract instead of silently normalising them. An API boundary is any call site outside the type's private surface (e.g., In .NET: every `public`, `protected`, and `internal` member; in TypeScript: every exported symbol). Private members may trust their callers.
- [INST0011] **Do** uphold invariants across an object's lifetime — every state a constructor or factory can produce must remain a state every subsequent operation accepts. Move the type between valid states; never through invalid ones.
- [INST0012] **Do** design types to be safe by construction — every constructor or factory must produce an instance already in a valid state, with no required follow-up call to make it usable. Avoid two-phase initialisation patterns like `new Foo()` then `foo.Initialize(...)`; either fold the second step into the constructor/factory, or expose only the factory and keep the constructor inaccessible. If construction needs work that the constructor can't do (async I/O, ordering with other objects), use an `async` static factory or a builder that returns a fully-initialised instance — never a half-built object the caller must finish.
- [INST0013] **Do** order parameters by relevance — the subject of the operation first (the thing being acted on or its identifier), then required modifiers in decreasing importance, then any callback or output parameters last. Required parameters must precede optional ones. A reader scanning a call site should be able to infer the call's intent from the first one or two arguments without consulting the signature.
- [INST0014] **Do** treat optional parameters as exceptional, not the default — every parameter is required unless there is a concrete justification (a parameter that is genuinely meaningful to omit at most call sites, or one whose absence has a well-defined, documented default behaviour that the caller should not have to think about). Adding "for convenience" or "because most callers don't need it" is not justification; if most callers do not need a parameter, that's a signal the parameter belongs on a different overload, a builder, or a separate type — not a default value on the main API. Two cases are canonically justified. The first is a conventional default established by the runtime (e.g., `CancellationToken ct = default` in .NET, where omitting the token means "never cancel") — the parameter is genuinely optional and the default carries an unambiguous meaning the caller doesn't need to think about. The second is an *options object* whose absence has a single well-defined meaning — "use the type's documented defaults" — where the parameter resolves to a defaults instance (e.g., `FooOptions? options = null` resolving to `options ?? new FooOptions()`); the justification is that the omitted value carries one unambiguous behaviour the caller need not reason about, not that it spares the caller from typing. A parameter whose default silently changes behaviour the caller *should* be reasoning about — a logger, a clock, a timeout window — is not either case and must be required.
- [INST0015] **Don't** widen a production API's visibility, add members, or change a member's signature just to make it reachable from tests (e.g., promoting `private` → `internal`/`protected` so tests can call it, adding test-only seams, opening setters). If a unit feels untestable through its public API, that's a design signal — refactor the dependency it's coupled to behind an interface, extract the hard-to-reach logic into its own publicly testable type, or inject collaborators via the constructor. The test pressure exists to drive the design change, not to be relieved by widening visibility.

### Diagnostics

- [INST0016] **Do** use Exception‑based Error Handling – wrap expected faults in `try/catch`; log unexpected ones.
- [INST0017] **Do** use Structured Logging when available.
