---
name: "web-nodejs (v1.0.0)"
description: "Apply when writing or reviewing Node.js server apps (module design, async patterns, process lifecycle, streams, security)."
applyTo: "**/*.{js,mjs,cjs,ts,mts,cts}"
---

# Node.js Instructions

## MCP Tool Validation

After editing or generating any TypeScript or JavaScript source file,
call the `analyze_typescript_code` MCP tool on the changed source.
Pass the file contents as `content` and the file's absolute path as
`originalPath`. Treat any reported violation as blocking — fix it
before reporting the work as done.

## Rules

### Async & Event Loop

- [INST0001] **Do** use `async`/`await` for all I/O — avoid callbacks and raw `.then()` chains.
- [INST0002] **Do** handle promise rejections — every `await` should be in a `try`/`catch` or the promise chain should have a `.catch()`; unhandled rejections terminate the process in modern Node.
- [INST0003] **Don't** block the event loop with synchronous I/O (`fs.readFileSync`, `crypto.pbkdf2Sync`) or CPU-heavy computation — offload to worker threads or a task queue.
- [INST0004] **Don't** use `process.exit()` in library code — let the caller or framework manage process lifecycle.

### Module Design

- [INST0005] **Do** use ES modules (`import`/`export`) for new code — avoid `require()` unless interoperating with CommonJS-only dependencies.
- [INST0006] **Do** keep modules focused — one responsibility per file; avoid god-files that initialize connections, define routes, and contain business logic.
- [INST0007] **Don't** rely on module-level side effects for initialization (e.g., opening a DB connection at import time) — use explicit init functions so callers control startup order.

### Security

- [INST0008] **Do** validate and sanitize all external input (query params, headers, body) at the boundary — use schemas (Zod, Joi, AJV) not ad-hoc checks.
- [INST0009] **Do** use parameterized queries for database access — never interpolate user input into SQL or NoSQL queries.
- [INST0010] **Do** set security headers (`helmet` or manual) — at minimum, disable `X-Powered-By` and set `Content-Security-Policy`, `Strict-Transport-Security`.
- [INST0011] **Don't** use `eval()`, `new Function()`, or `child_process.exec()` with unsanitized input — use `execFile` or `spawn` with explicit argument arrays.

### Process Lifecycle

- [INST0012] **Do** handle `SIGTERM` and `SIGINT` for graceful shutdown — close server listeners, drain connections, flush logs, then exit.
- [INST0013] **Do** use structured logging (`pino`, `winston` with JSON) — avoid `console.log` in production code.
- [INST0014] **Don't** store application state in process memory that would be lost on restart — use an external store (Redis, database) for anything that must survive deploys.
