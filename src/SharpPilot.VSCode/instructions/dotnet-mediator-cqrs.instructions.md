---
description: "Use when implementing CQRS patterns, MediatR handlers, pipelines, or command/query separation in .NET."
applyTo: "**/*.{cs,fs,vb}"
---
# Mediator / CQRS Guidelines

## Command & Query Separation

- **Do** separate commands (write) from queries (read) — a single handler should not both mutate state and return projected data.
- **Do** keep commands and queries as simple DTOs (records or classes with public properties) — no behavior or injected services on the request object itself.
- **Do** name requests after intent: `CreateOrderCommand`, `GetOrderByIdQuery` — the name should describe the operation, not the handler.

## Handler Design

- **Do** keep handlers focused on a single operation — if a handler grows beyond orchestration, extract domain logic into a dedicated service.
- **Do** accept `CancellationToken` in every handler and pass it to all async calls downstream.
- **Don't** inject `IMediator` / `ISender` into handlers to dispatch more requests — it creates hidden coupling chains; call services directly or compose in the application layer.
- **Don't** use the mediator as a general-purpose service locator — it is for dispatching requests through a pipeline, not for resolving arbitrary dependencies.

## Pipeline Behaviors

- **Do** use pipeline behaviors for cross-cutting concerns (validation, logging, transaction wrapping) — keep them thin and composable.
- **Do** order behaviors deliberately: validation before execution, transaction wrapping around the handler, logging at the outermost layer.
- **Don't** put business logic in pipeline behaviors — they should enforce policy, not implement features.

## Notifications

- **Do** use notifications for fire-and-forget side effects (audit logging, cache invalidation, event forwarding) — never for operations that the caller depends on.
- **Don't** assume notification handler execution order — handlers run in registration order but should be independent of each other.
