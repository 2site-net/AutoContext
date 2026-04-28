---
name: "lang-scala (v1.0.0)"
description: "Apply when writing or reviewing Scala code (naming, type design, pattern matching, implicits, collections, concurrency)."
applyTo: "**/*.{scala,sc}"
---

# Scala Coding Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Naming

- [INST0001] **Do** use PascalCase for classes, traits, objects, and type aliases; use camelCase for methods, values, variables, and parameters.
- [INST0002] **Do** use PascalCase for constants defined in companion objects or package objects — `val MaxRetryCount = 3` — not UPPER_SNAKE_CASE (Scala convention).
- [INST0003] **Do** name packages in all-lowercase with dots as separators — follow the reversed-domain convention (e.g., `com.example.billing.api`).
- [INST0004] **Do** name boolean values and methods with affirmative predicates — `isValid`, `hasAccess`, `canRetry` — not negations like `isNotEmpty`.
- [INST0005] **Don't** encode types or roles in names — Scala's expressive type system and type inference make prefixes like `obj`, `str`, or `lst` redundant; write `val account: Account`, not `val objAccount`.

### Type Design

- [INST0006] **Do** use `case class` for immutable value types whose identity is defined by their fields — `equals`, `hashCode`, `copy`, and `toString` are generated automatically.
- [INST0007] **Do** use `sealed trait` or `sealed abstract class` when the set of subtypes is fixed and known at compile time — enables exhaustive pattern matching.
- [INST0008] **Do** prefer `val` over `var` — immutability is the default; mutability should be explicit and intentional.
- [INST0009] **Do** program to abstractions — declare parameters as `Seq`, `Iterable`, or `Map` when the method does not depend on a specific collection type; use concrete types (`List`, `Vector`) when performance characteristics matter.
- [INST0010] **Do** use `opaque type` (Scala 3) or `AnyVal` wrapper classes (Scala 2) to create zero-cost typed wrappers around primitives (e.g., `opaque type CustomerId = Long`).
- [INST0011] **Do** use `enum` (Scala 3) or `sealed trait` with `case object` subtypes (Scala 2) for algebraic data types with a fixed set of values.
- [INST0012] **Don't** use `null` — use `Option[T]` when a value may be absent, `Either[E, A]` for computations that can fail, or `Try[T]` for exception-prone code.

### Pattern Matching

- [INST0013] **Do** use pattern matching (`match`) over chains of `if`/`else` when destructuring or branching on type — it is clearer and compiler-checked for sealed hierarchies.
- [INST0014] **Do** handle all branches exhaustively when matching on sealed types — avoid wildcard (`_`) catch-all patterns unless genuinely all remaining cases share the same behaviour.
- [INST0015] **Do** use pattern matching in `val` bindings for simple destructuring — `val (name, age) = person` — but not for complex or fallible patterns.
- [INST0016] **Do** use guard clauses (`case x if x > 0 =>`) instead of nested `if` expressions inside match arms.
- [INST0017] **Don't** use pattern matching as a replacement for polymorphism — if you're matching on the same sealed hierarchy in many places, add a method to the trait instead.

### Implicits & Contextual Abstractions

- [INST0018] **Do** use `given`/`using` (Scala 3) or `implicit` parameters (Scala 2) for type class instances, execution contexts, and cross-cutting capabilities — not for arbitrary dependency injection.
- [INST0019] **Do** use extension methods (Scala 3 `extension` keyword, Scala 2 `implicit class`) to add behaviour to types you don't own — prefer them over utility objects with static-like methods.
- [INST0020] **Do** place implicit/given instances in the companion object of the type class or the type being extended — callers find them automatically via implicit scope.
- [INST0021] **Don't** use implicit conversions — they obscure control flow and make code harder to reason about; use extension methods instead.

### Collections

- [INST0022] **Do** use immutable collections by default (`List`, `Vector`, `Map`, `Set`); use `mutable.Buffer`, `mutable.Map`, `mutable.Set` only when mutation is required for performance.
- [INST0023] **Do** prefer collection operations (`map`, `filter`, `flatMap`, `fold`, `collect`, `groupBy`) over manual loops when the intent is clearer.
- [INST0024] **Do** use `LazyList` (Scala 2.13+/3) or `Iterator` for lazy evaluation when working with large data sets or when not all elements will be consumed.
- [INST0025] **Do** use `collect` with partial functions to filter and transform in a single pass — `items.collect { case Valid(x) => x }`.
- [INST0026] **Don't** call `.toList`, `.toVector`, or other eager conversions inside a chain of lazy operations — defer materialization to the final step.

### Error Handling

- [INST0027] **Do** use `Option[T]` for values that may be absent, `Either[E, A]` for recoverable domain errors, and `Try[T]` only when wrapping code that throws exceptions.
- [INST0028] **Do** use `for`-comprehensions to chain `Option`, `Either`, or `Try` operations — they flatten the error handling and keep the happy path readable.
- [INST0029] **Do** use `recover` and `recoverWith` on `Future` and `Try` to handle expected failure cases at boundaries.
- [INST0030] **Don't** throw exceptions for business logic errors — reserve `throw` for genuinely unrecoverable programmer errors; model domain errors as values.

### Concurrency

- [INST0031] **Do** use `Future` with an explicit `ExecutionContext` for asynchronous computations — never import `scala.concurrent.ExecutionContext.Implicits.global` in production code; inject the context instead.
- [INST0032] **Do** compose `Future` values with `map`, `flatMap`, and `for`-comprehensions — avoid nested callbacks.
- [INST0033] **Do** use `Future.sequence` or `Future.traverse` to run independent tasks concurrently and collect results.
- [INST0034] **Don't** use `Await.result` except in tests or at the top-level entry point — blocking defeats the purpose of asynchronous programming.

### Documentation

- [INST0035] **Do** add Scaladoc (`/** … */`) to all public and protected types, methods, and values — document intent, parameters (`@param`), return value (`@return`), and thrown exceptions (`@throws`).
- [INST0036] **Don't** add Scaladoc that merely restates the method name — `/** Gets the name. */ def getName: String` adds no value.
