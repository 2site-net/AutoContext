---
description: "Use when building ASP.NET Core Web APIs: middleware pipeline, routing, error handling, authentication, model validation, HttpClient, and minimal vs controller-based APIs."
applyTo: "**/*.cs"
---
# ASP.NET Core / Web API Guidelines

- **Do** keep endpoint handlers and controller actions thin — delegate business logic to injected services; the handler should only map HTTP concerns (route/query params, status codes) to domain calls.
- **Do** register middleware in the documented order: `UseForwardedHeaders` → `UseHttpsRedirection` → `UseStaticFiles` → `UseRouting` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseAntiforgery` → `UseRateLimiter` → endpoints. Misordering `UseAuthorization` before `UseRouting` silently disables endpoint authorization.
- **Do** use `UseExceptionHandler` (or `IExceptionHandler`) and return RFC 9457 Problem Details (`ProblemDetails`/`ValidationProblemDetails`) for all error responses — never expose raw exception details outside the `Development` environment.
- **Do** validate request models with Data Annotations or `IValidatableObject` — invalid input should never reach business logic.
- **Do** use `IHttpClientFactory` (or typed/named clients) to obtain `HttpClient` instances — creating and disposing `HttpClient` directly exhausts sockets and causes DNS stale-entry bugs.
- **Do** accept `CancellationToken` in endpoint handlers and controller actions and pass it through to async calls — the token fires when the client disconnects, allowing early cancellation of database queries and HTTP calls.
- **Do** use policy-based authorization (`[Authorize(Policy = "…")]` / `RequireAuthorization("…")`) rather than role checks scattered through code — policies centralise access rules and are easier to test and audit.
- **Do** use `IAsyncEnumerable<T>` or pagination for large result sets — materialising an entire collection into memory and serialising it in one response harms latency, memory, and throughput.
- **Do** offload long-running work to a background service (`BackgroundService`/`IHostedService`) or a message queue — never block the HTTP request thread waiting for CPU-intensive or minutes-long operations.
- **Do** make all endpoint handlers and controller actions `async` and call data access / I/O APIs asynchronously — synchronous reads or writes on `HttpRequest`/`HttpResponse` bodies can starve the thread pool under load.
- **Don't** enable the Developer Exception Page in production — it exposes stack traces, source code, and environment variables; use `UseExceptionHandler` with a generic error payload instead.
- **Don't** call `Task.Run` and immediately `await` it inside a request handler — ASP.NET Core already runs on thread-pool threads, so the extra scheduling adds overhead without benefit.
- **Don't** store per-request state in `static` fields or singleton services — ASP.NET Core processes many requests concurrently; use `Scoped` services or `HttpContext.Items` for request-scoped data.

## Minimal APIs

- **Do** prefer Minimal APIs (`app.MapGet`, `app.MapPost`, …) for new endpoints — they have less overhead, simpler testing, and less boilerplate than controller-based APIs. Reserve controllers when you need advanced model binding, `JsonPatch`, or OData support.
- **Do** return `TypedResults` (e.g., `TypedResults.Ok(value)`, `TypedResults.NotFound()`) instead of `Results` — typed results carry compile-time response metadata for OpenAPI generation and enable type assertions in tests.
- **Do** use `RouteGroupBuilder` (`app.MapGroup("/api/users")`) to share prefixes, filters, and metadata across related endpoints — avoids repeating `.RequireAuthorization()` and `.WithTags()` on every endpoint.
- **Do** extract endpoint definitions into static extension methods (e.g., `MapUserEndpoints(this IEndpointRouteBuilder)`) once a group exceeds roughly five endpoints — keeps `Program.cs` readable.
- **Do** use `WithName()` and `WithTags()` on every endpoint — `WithName` enables link generation via `LinkGenerator` and `WithTags` organises OpenAPI documentation.
- **Do** use endpoint filters (`AddEndpointFilter<T>`) for cross-cutting concerns (validation, logging, auth checks) — they are the Minimal API equivalent of action filters and follow the same onion model. For model validation, use `.WithParameterValidation()` (via `MinimalApis.Extensions`) or a custom validation filter.
- **Do** use `[AsParameters]` to bind multiple route, query, and header values into a single record — reduces parameter clutter and makes the handler signature readable.
- **Do** implement `IBindableFromHttpContext<T>` or a static `BindAsync` method for custom complex types that don't fit default binding — avoids manual parsing in every handler.
- **Do** annotate endpoints with `Produces<T>(statusCode)` and `ProducesValidationProblem()` when the response types cannot be inferred — explicit metadata gives accurate OpenAPI documentation.
- **Do** test Minimal API endpoints via `WebApplicationFactory<Program>` and `HttpClient` — this exercises the full middleware pipeline including filters, auth, and serialisation.
- **Don't** use `[FromForm]` without antiforgery — form-based submissions are vulnerable to CSRF unless antiforgery validation is enabled.
