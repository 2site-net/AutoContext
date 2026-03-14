---
description: "Use when designing C# systems: SOLID principles, dependency injection, composition, separation of concerns, error handling, and structured logging."
---
# Design Principles

- **Do** use Separation of Concerns – keep data, domain and UI in distinct layers; keep UI types out of core code.
- **Do** use Dependency Injection – prefer constructor injection; avoid service locators and singletons.
- **Do** favor Composition over inheritance; expose clear extension points.
- **Do** follow SOLID (SRP · OCP · LSP · ISP · DIP).
- **Do** use Exception‑based Error Handling – wrap expected faults in `try/catch`; log unexpected ones.
- **Do** design for Testability First – isolate components and minimise mocks.
- **Do** use Structured Logging – rely on Serilog (or similar) with proper log levels when available.
