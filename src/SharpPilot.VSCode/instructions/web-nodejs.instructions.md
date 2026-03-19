---
description: "Use when building Node.js server applications: module design, async patterns, process lifecycle, streams, and security."
applyTo: "**/*.{js,mjs,cjs,ts,mts,cts}"
---
# Node.js Guidelines

## Async & Event Loop

- **Do** use `async`/`await` for all I/O — avoid callbacks and raw `.then()` chains.
- **Do** handle promise rejections — every `await` should be in a `try`/`catch` or the promise chain should have a `.catch()`; unhandled rejections terminate the process in modern Node.
- **Don't** block the event loop with synchronous I/O (`fs.readFileSync`, `crypto.pbkdf2Sync`) or CPU-heavy computation — offload to worker threads or a task queue.
- **Don't** use `process.exit()` in library code — let the caller or framework manage process lifecycle.

## Module Design

- **Do** use ES modules (`import`/`export`) for new code — avoid `require()` unless interoperating with CommonJS-only dependencies.
- **Do** keep modules focused — one responsibility per file; avoid god-files that initialize connections, define routes, and contain business logic.
- **Don't** rely on module-level side effects for initialization (e.g., opening a DB connection at import time) — use explicit init functions so callers control startup order.

## Security

- **Do** validate and sanitize all external input (query params, headers, body) at the boundary — use schemas (Zod, Joi, AJV) not ad-hoc checks.
- **Do** use parameterized queries for database access — never interpolate user input into SQL or NoSQL queries.
- **Do** set security headers (`helmet` or manual) — at minimum, disable `X-Powered-By` and set `Content-Security-Policy`, `Strict-Transport-Security`.
- **Don't** use `eval()`, `new Function()`, or `child_process.exec()` with unsanitized input — use `execFile` or `spawn` with explicit argument arrays.

## Process Lifecycle

- **Do** handle `SIGTERM` and `SIGINT` for graceful shutdown — close server listeners, drain connections, flush logs, then exit.
- **Do** use structured logging (`pino`, `winston` with JSON) — avoid `console.log` in production code.
- **Don't** store application state in process memory that would be lost on restart — use an external store (Redis, database) for anything that must survive deploys.
