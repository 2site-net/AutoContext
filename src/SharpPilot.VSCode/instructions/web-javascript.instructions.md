---
description: "Use when generating or editing JavaScript or TypeScript code, working with ES modules, async patterns, DOM manipulation, or modern JS/TS features."
applyTo: "**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}"
---
# JavaScript Guidelines

> These rules target JavaScript code — language fundamentals, async patterns, modern features, and security.

* **Do** use `const` by default; use `let` only when reassignment is needed — signals intent and prevents accidental mutation.
* **Do** use `===` and `!==` instead of `==` and `!=` — strict equality avoids implicit type coercion bugs.
* **Do** use template literals for string interpolation and multi-line strings instead of string concatenation.
* **Do** use ES modules (`import`/`export`) over CommonJS (`require`/`module.exports`) — they are statically analyzable and tree-shakeable.
* **Do** use optional chaining (`?.`) and nullish coalescing (`??`) for safe property access and defaults instead of manual `&&` chains or `||` fallbacks.
* **Do** use `camelCase` for variables and functions, `PascalCase` for classes, and `UPPER_SNAKE_CASE` for module-level constants.
* **Do** keep functions short and single-purpose — extract when a function does more than one thing or exceeds a screenful.
* **Do** prefer `async`/`await` over raw `.then()` chains for readability — wrap awaited calls in `try`/`catch` for error handling.
* **Do** use `Promise.all()` or `Promise.allSettled()` for independent concurrent operations instead of sequential `await`s in a loop.
* **Do** use `structuredClone()` for deep-cloning objects instead of `JSON.parse(JSON.stringify())` — handles `Date`, `Map`, `Set`, `ArrayBuffer`, and circular references.
* **Do** use array methods (`map`, `filter`, `find`, `some`, `every`, `reduce`) over manual `for` loops when they express intent more clearly.
* **Do** use destructuring for extracting properties from objects and arrays — reduces repetition and makes bindings explicit.
* **Do** throw `Error` objects (or subclasses), never strings or plain objects — stack traces and `instanceof` checks only work with `Error`.
* **Do** add a `.catch()` or `try`/`catch` on every Promise chain — unhandled rejections crash Node.js and produce silent failures in browsers.
* **Do** use `textContent` (or a sanitizer like DOMPurify) when inserting user-supplied content into the DOM — prevents XSS via safe sink selection.
* **Do** validate `event.origin` inside `message` event listeners before processing `postMessage` data — untrusted origins can inject malicious payloads.
* **Do** pass an `AbortController` signal to `fetch()` and event listeners, and call `abort()` on cleanup — prevents memory leaks and dangling requests after a component or operation is torn down.
* **Do** use `Number.isNaN()` instead of the global `isNaN()` — the global version coerces its argument to a number first, producing false positives (e.g., `isNaN("hello")` is `true`).
* **Do** always pass the radix to `parseInt()` (e.g., `parseInt(str, 10)`) — omitting it can cause octal or hex interpretation of ambiguous strings.
* **Do** check `response.ok` after `fetch()` before reading the body — `fetch` only rejects on network errors, not on HTTP 4xx/5xx status codes.
* **Do** use `Object.hasOwn(obj, key)` instead of `obj.hasOwnProperty(key)` — works on objects created with `Object.create(null)` and is more concise.
* **Do** use classes for stateful objects with behavior and lifecycle; prefer plain functions and modules for stateless operations and utilities — avoid wrapping a single function in a class.
* **Do** insert a blank line before control flow statements (`if`, `for`, `for...of`, `for...in`, `while`, `do`, `switch`, `try`, `using`).
* **Do** insert a blank line between variable declarations and their first usage.
* **Don't** use `var` — it has function scope, hoisting quirks, and allows accidental redeclaration; use `const` or `let` instead.
* **Don't** use `for...in` to iterate arrays or iterables — it enumerates all enumerable properties including inherited ones; use `for...of` or array methods instead.
* **Don't** use `eval()`, `new Function()`, or `setTimeout`/`setInterval` with string arguments — they parse strings as code and open code-injection vectors.
* **Don't** use `innerHTML`, `outerHTML`, or `document.write()` with unsanitized data — use `textContent` for text or sanitize with DOMPurify first.
* **Don't** use arrow functions as object methods or constructors — they don't bind their own `this`, leading to unexpected `undefined` values.
* **Don't** mutate function parameters — copy first (spread, `structuredClone`) if modification is needed, to avoid surprising callers.
* **Don't** leave empty `catch` blocks — at minimum log the error or add a comment explaining why the error is intentionally swallowed.
* **Don't** use the `arguments` object — use rest parameters (`...args`) instead; they produce a real array, work in arrow functions, and make the accepted parameters explicit.
