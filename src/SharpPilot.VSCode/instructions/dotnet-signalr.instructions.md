---
description: "Use when building SignalR hubs, real-time connections, group management, or backplane configuration in .NET."
applyTo: "**/*.{cs,fs,vb}"
---
# SignalR Guidelines

## Hub Design

- **Do** keep hubs as thin dispatchers — inject services for business logic rather than implementing it in hub methods.
- **Do** use strongly-typed hubs (`Hub<IClientProxy>`) to get compile-time safety on client method names.
- **Do** use `[Authorize]` or any known project-specific attribute that derives from it on hubs or individual methods to enforce authentication.
- **Don't** store per-connection state in hub instance fields — hubs are transient; use `Groups`, `ConnectionId` mapping, or an external store.

## Groups & Connections

- **Do** use groups for topic-based broadcasting (e.g., `Clients.Group("room-42").SendAsync(...)`) — avoid iterating connection IDs manually.
- **Do** track connection lifecycle via `OnConnectedAsync` / `OnDisconnectedAsync` for presence tracking or resource cleanup.
- **Don't** assume a single connection per user — a user can have multiple tabs/devices; use groups or `Clients.User(userId)` for user-level messaging.

## Scaling & Reliability

- **Do** configure a backplane (Redis, Azure SignalR Service, or SQL Server) when running behind a load balancer — without it, messages only reach clients connected to the same server.
- **Do** handle reconnection gracefully on the client — implement `onreconnecting` / `onreconnected` callbacks and re-join groups after reconnect.
- **Don't** send large payloads over SignalR — it is designed for small, frequent messages; use a download endpoint for bulk data.
- **Don't** use SignalR for request/response patterns — use REST or gRPC for operations where the caller needs a direct reply.
