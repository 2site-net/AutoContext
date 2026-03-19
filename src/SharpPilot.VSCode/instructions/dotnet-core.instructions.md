---
description: "Use when writing, reviewing, or refactoring .NET dependency injection, logging, configuration, or security setup."
applyTo: "**/*.{cs,fs,vb}"
---
# .NET Core Guidelines

## Dependency Injection

- **Do** use constructor injection — avoid pulling services from `IServiceProvider` directly outside of factories.
- **Do** organize DI registrations into feature-scoped `ServiceCollectionExtensions` classes — one per feature folder, not one big file in `Program.cs`.
- **Do** default to `Scoped` for business services (per-request state) and `Singleton` for stateless infrastructure (evaluators, registries, config facades).
- **Do** register against interfaces, not concrete types — keep consumers decoupled from implementations.
- **Do** use `TryAdd*` / `TryAddEnumerable` in reusable library code to avoid duplicate registrations when modules are composed.
- **Don't** inject `IServiceProvider` into business logic — it hides dependencies and makes testing harder.
- **Don't** capture a `Scoped` service inside a `Singleton` — it causes lifetime mismatch bugs.

## Logging

- **Do** inject `ILogger<T>` via constructor — never use `LoggerFactory.Create` or static loggers.
- **Do** use `[LoggerMessage]` source-generated partial methods for structured logging — they are higher performance, AOT-friendly, and avoid boxing allocations compared to the `ILogger` extension methods.
- **Do** choose log levels deliberately: `Debug` for dev diagnostics, `Information` for business events, `Warning` for recoverable issues, `Error` for failures.
- **Do** use `logger.BeginScope(…)` to attach ambient context (e.g., request ID, user ID) across a multi-step operation.
- **Don't** log sensitive data (passwords, tokens, PII) — scrub or omit before logging.

## Configuration

- **Do** bind configuration to strongly-typed options classes with `services.Configure<T>(…)` — don't scatter `IConfiguration["key"]` reads through business logic.
- **Do** use `IOptions<T>` for singleton lifetime, `IOptionsSnapshot<T>` for scoped (per-request reload), `IOptionsMonitor<T>` for singleton with live reload.
- **Do** validate options at startup with `ValidateDataAnnotations()` and `ValidateOnStart()` to catch missing or invalid config early.
- **Don't** store secrets in `appsettings.json` — use environment variables, .NET Secret Manager (dev), or Azure Key Vault (production).

## Security

- **Do** use `AddAuthentication` / `AddAuthorization` — never implement custom auth middleware from scratch.
- **Do** apply authorization declaratively at the endpoint level using `[Authorize]` or any known project-specific attribute that derives from it; use `[AllowAnonymous]` to explicitly opt out.
- **Do** use `IDataProtectionProvider` to protect sensitive data at rest (e.g., tokens, cookies).
- **Do** use HTTPS redirection middleware (`UseHttpsRedirection`) and HSTS (`UseHsts`) in production.
- **Don't** store secrets or connection strings in source-controlled config files — keep them in environment variables or a secrets store.
