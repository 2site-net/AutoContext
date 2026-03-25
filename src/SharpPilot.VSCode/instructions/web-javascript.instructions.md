---
description: "Use when generating or editing JavaScript or TypeScript code, working with ES modules, async patterns, DOM manipulation, or modern JS/TS features."
applyTo: "**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}"
---
# JavaScript Guidelines

> These instructions target JavaScript code — language fundamentals, async patterns, modern features, and security.

- [INST0001] **Do** use `const` by default; use `let` only when reassignment is needed — signals intent and prevents accidental mutation.
- [INST0002] **Do** use `===` and `!==` instead of `==` and `!=` — strict equality avoids implicit type coercion bugs.
- [INST0003] **Do** use template literals for string interpolation and multi-line strings instead of string concatenation.
- [INST0004] **Do** use ES modules (`import`/`export`) over CommonJS (`require`/`module.exports`) — they are statically analyzable and tree-shakeable.
- [INST0005] **Do** use optional chaining (`?.`) and nullish coalescing (`??`) for safe property access and defaults instead of manual `&&` chains or `||` fallbacks.
- [INST0006] **Do** use `camelCase` for variables and functions, `PascalCase` for classes, and `UPPER_SNAKE_CASE` for module-level constants.
- [INST0007] **Do** keep functions short and single-purpose — extract when a function does more than one thing or exceeds a screenful.
- [INST0008] **Do** prefer `async`/`await` over raw `.then()` chains for readability — wrap awaited calls in `try`/`catch` for error handling.
- [INST0009] **Do** use `Promise.all()` or `Promise.allSettled()` for independent concurrent operations instead of sequential `await`s in a loop.
- [INST0010] **Do** use `structuredClone()` for deep-cloning objects instead of `JSON.parse(JSON.stringify())` — handles `Date`, `Map`, `Set`, `ArrayBuffer`, and circular references.
- [INST0011] **Do** use array methods (`map`, `filter`, `find`, `some`, `every`, `reduce`) over manual `for` loops when they express intent more clearly.
- [INST0012] **Do** use destructuring for extracting properties from objects and arrays — reduces repetition and makes bindings explicit.
- [INST0013] **Do** throw `Error` objects (or subclasses), never strings or plain objects — stack traces and `instanceof` checks only work with `Error`.
- [INST0014] **Do** add a `.catch()` or `try`/`catch` on every Promise chain — unhandled rejections crash Node.js and produce silent failures in browsers.
- [INST0015] **Do** use `textContent` (or a sanitizer like DOMPurify) when inserting user-supplied content into the DOM — prevents XSS via safe sink selection.
- [INST0016] **Do** validate `event.origin` inside `message` event listeners before processing `postMessage` data — untrusted origins can inject malicious payloads.
- [INST0017] **Do** pass an `AbortController` signal to `fetch()` and event listeners, and call `abort()` on cleanup — prevents memory leaks and dangling requests after a component or operation is torn down.
- [INST0018] **Do** use `Number.isNaN()` instead of the global `isNaN()` — the global version coerces its argument to a number first, producing false positives (e.g., `isNaN("hello")` is `true`).
- [INST0019] **Do** always pass the radix to `parseInt()` (e.g., `parseInt(str, 10)`) — omitting it can cause octal or hex interpretation of ambiguous strings.
- [INST0020] **Do** check `response.ok` after `fetch()` before reading the body — `fetch` only rejects on network errors, not on HTTP 4xx/5xx status codes.
- [INST0021] **Do** use `Object.hasOwn(obj, key)` instead of `obj.hasOwnProperty(key)` — works on objects created with `Object.create(null)` and is more concise.
- [INST0022] **Do** use classes for stateful objects with behavior and lifecycle; prefer plain functions and modules for stateless operations and utilities — avoid wrapping a single function in a class.
- [INST0023] **Do** insert a blank line before control flow statements (`if`, `for`, `for...of`, `for...in`, `while`, `do`, `switch`, `try`, `using`).
- [INST0024] **Do** insert a blank line between variable declarations and their first usage.
- [INST0025] **Do** keep each module focused on a single concept — split when a file mixes unrelated types, data, and logic; a module that serves as a grab-bag of utilities becomes hard to name, navigate, and reason about.
- [INST0026] **Do** place a helper function in its sole consumer's module rather than a shared utility file — extract to a shared module only when a second consumer appears.
- [INST0027] **Do** make module-private helper functions `private static` methods when they serve a single class in the same file — keeps the module's public surface minimal and makes ownership explicit.
- [INST0028] **Don't** use `var` — it has function scope, hoisting quirks, and allows accidental redeclaration; use `const` or `let` instead.
- [INST0029] **Don't** use `for...in` to iterate arrays or iterables — it enumerates all enumerable properties including inherited ones; use `for...of` or array methods instead.
- [INST0030] **Don't** use `eval()`, `new Function()`, or `setTimeout`/`setInterval` with string arguments — they parse strings as code and open code-injection vectors.
- [INST0031] **Don't** use `innerHTML`, `outerHTML`, or `document.write()` with unsanitized data — use `textContent` for text or sanitize with DOMPurify first.
- [INST0032] **Don't** use arrow functions as object methods or constructors — they don't bind their own `this`, leading to unexpected `undefined` values.
- [INST0033] **Don't** mutate function parameters — copy first (spread, `structuredClone`) if modification is needed, to avoid surprising callers.
- [INST0034] **Don't** leave empty `catch` blocks — at minimum log the error or add a comment explaining why the error is intentionally swallowed.
- [INST0035] **Don't** use the `arguments` object — use rest parameters (`...args`) instead; they produce a real array, work in arrow functions, and make the accepted parameters explicit.
- [INST0036] **Don't** re-export symbols through intermediate barrel modules just to preserve an import path — update consumers to import directly from the owning module; passthrough re-exports add indirection without value.
