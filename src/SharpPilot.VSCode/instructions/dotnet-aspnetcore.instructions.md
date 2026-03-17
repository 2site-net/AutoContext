---
description: "Use when building ASP.NET Core Web APIs: middleware pipeline, routing, error handling, authentication, model validation, HttpClient, and minimal vs controller-based APIs."
applyTo: "**/*.cs"
---
# ASP.NET Core / Web API Guidelines

- **Do** prefer Minimal APIs (`app.MapGet`, `app.MapPost`, …) for new endpoints — they have less overhead, simpler testing, and less boilerplate than controller-based APIs. Reserve controllers when you need advanced model binding, `JsonPatch`, or OData support.
- **Do** keep endpoint handlers thin — delegate business logic to injected services; the handler should only map HTTP concerns (route/query params, status codes) to domain calls.
- **Do** return `TypedResults` (e.g., `TypedResults.Ok(value)`, `TypedResults.NotFound()`) instead of `Results` — typed results carry compile-time response metadata for OpenAPI generation and enable type assertions in tests.
- **Do** register middleware in the documented order: `UseForwardedHeaders` → `UseHttpsRedirection` → `UseStaticFiles` → `UseRouting` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseAntiforgery` → `UseRateLimiter` → endpoints. Misordering `UseAuthorization` before `UseRouting` silently disables endpoint authorization.
- **Do** use `UseExceptionHandler` (or `IExceptionHandler`) and return RFC 9457 Problem Details (`ProblemDetails`/`ValidationProblemDetails`) for all error responses — never expose raw exception details outside the `Development` environment.
- **Do** validate request models with Data Annotations or `IValidatableObject`; for Minimal APIs, add an endpoint filter or use `.WithParameterValidation()` (via `MinimalApis.Extensions`) — invalid input should never reach business logic.
- **Do** use `IHttpClientFactory` (or typed/named clients) to obtain `HttpClient` instances — creating and disposing `HttpClient` directly exhausts sockets and causes DNS stale-entry bugs.
- **Do** accept `CancellationToken` in endpoint handlers and pass it through to async calls — the token fires when the client disconnects, allowing early cancellation of database queries and HTTP calls.
- **Do** use policy-based authorization (`[Authorize(Policy = "…")]` / `RequireAuthorization("…")`) rather than role checks scattered through code — policies centralise access rules and are easier to test and audit.
- **Do** use `IAsyncEnumerable<T>` or pagination for large result sets — materialising an entire collection into memory and serialising it in one response harms latency, memory, and throughput.
- **Do** offload long-running work to a background service (`BackgroundService`/`IHostedService`) or a message queue — never block the HTTP request thread waiting for CPU-intensive or minutes-long operations.
- **Do** make all endpoint handlers `async` and call data access / I/O APIs asynchronously — synchronous reads or writes on `HttpRequest`/`HttpResponse` bodies can starve the thread pool under load.
- **Don't** enable the Developer Exception Page in production — it exposes stack traces, source code, and environment variables; use `UseExceptionHandler` with a generic error payload instead.
- **Don't** call `Task.Run` and immediately `await` it inside a request handler — ASP.NET Core already runs on thread-pool threads, so the extra scheduling adds overhead without benefit.
- **Don't** store per-request state in `static` fields or singleton services — ASP.NET Core processes many requests concurrently; use `Scoped` services or `HttpContext.Items` for request-scoped data.
