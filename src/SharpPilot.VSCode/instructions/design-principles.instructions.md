---
description: "Use when designing software systems: SOLID principles, dependency injection, composition, separation of concerns, error handling, and structured logging."
---
# Design Principles

- **Do** give descriptive names that reveal intent — prefer `retriesRemaining` over `n`, `calculateMonthlyTotal` over `calc`, `getUserById` over `getUser`, and `setContext` over `setCtx`; single-letter names are acceptable only as loop counters or well-known math symbols.
- **Do** use Separation of Concerns – keep data, domain and UI in distinct layers; keep UI types out of core code.
- **Do** use Dependency Injection – prefer constructor injection; avoid service locators and singletons.
- **Do** favor Composition over inheritance; expose clear extension points.
- **Do** follow SOLID (SRP · OCP · LSP · ISP · DIP).
- **Do** use Exception‑based Error Handling – wrap expected faults in `try/catch`; log unexpected ones.
- **Do** design for Testability First – isolate components and minimise mocks.
- **Do** use Structured Logging when available.
