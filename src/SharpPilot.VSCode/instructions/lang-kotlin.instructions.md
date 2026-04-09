---
description: "Kotlin coding style instructions: naming conventions, null safety, type design, scope functions, collections, coroutines, and documentation."
applyTo: "**/*.{kt,kts}"
---
# Kotlin Coding Style

## Naming

- [INST0001] **Do** use PascalCase for classes, interfaces, objects, enums, and type aliases; use camelCase for functions, properties, and local variables.
- [INST0002] **Do** use SCREAMING_SNAKE_CASE for `const val` declarations in companion objects and top-level scope.
- [INST0003] **Do** name packages in all-lowercase with dots as separators — follow the reversed-domain convention (e.g., `com.example.billing.api`).
- [INST0004] **Do** name boolean properties and functions with affirmative predicates — `isValid`, `hasAccess`, `canRetry` — not negations like `isNotEmpty`.
- [INST0005] **Don't** carry over `m`-prefix (Android-era Java) or type-prefix conventions — write `val account: Account`, not `val mAccount` or `val objAccount`.

## Null Safety

- [INST0006] **Do** design APIs to be non-nullable by default — use `T?` only when `null` is a meaningful value, not as a substitute for a missing value.
- [INST0007] **Do** use safe-call (`?.`) and `let` for nullable operations — `value?.let { process(it) }` — instead of explicit null checks followed by usage.
- [INST0008] **Do** use the Elvis operator (`?:`) for early exits — `val name = input ?: return` or `val name = input ?: throw IllegalArgumentException("name required")`.
- [INST0009] **Do** use `requireNotNull` and `checkNotNull` when a null value at a validation boundary indicates a programming error — they produce clear exceptions.
- [INST0010] **Don't** use `!!` except at system boundaries where a null value is genuinely impossible and worth documenting — each use is a potential `NullPointerException`.
- [INST0011] **Don't** use `Optional<T>` — Kotlin's nullable type system (`T?`) subsumes it entirely.

## Type Design

- [INST0012] **Do** use `data class` for value objects whose identity is defined by their properties — `equals`, `hashCode`, `copy`, and `toString` are generated automatically.
- [INST0013] **Do** use `sealed class` or `sealed interface` when the set of subtypes is fixed and known at compile time — enables exhaustive `when` expressions.
- [INST0014] **Do** use `object` for singletons and `companion object` for factory methods and class-level constants.
- [INST0015] **Do** prefer `val` over `var` — mutability should be explicit and intentional.
- [INST0016] **Do** program to interfaces — declare properties, parameters, and return types as `List`, `Map`, `Set`, not `ArrayList`, `HashMap`, `HashSet`.
- [INST0017] **Do** use `@JvmInline value class` to wrap primitives for type safety (e.g., `value class CustomerId(val id: Long)`) — eliminates boxing overhead.

## Language Features

- [INST0018] **Do** use `when` as an expression when producing a result — exhaustively handle all branches for sealed types and enums.
- [INST0019] **Do** rely on smart casts — after an `is` check, no explicit cast is required; use it instead of `as`.
- [INST0020] **Do** use extension functions to add behaviour to types you don't own — prefer them over utility classes with static methods.
- [INST0021] **Do** use destructuring declarations with data classes (`val (name, age) = person`) and collection iteration (`for ((key, value) in map)`).
- [INST0022] **Do** use string templates (`"Hello, $name"`, `"Result: ${compute()}"`) instead of string concatenation.
- [INST0023] **Don't** use `as` for unsafe casts — use `as?` and handle the nullable result, or restructure so the type is known statically.

## Scope Functions

- [INST0024] **Do** use `apply` for object initialization — configure an object via its members and return the object itself.
- [INST0025] **Do** use `let` for null-safe transformations — operate on a non-null value within the lambda and return the lambda's result.
- [INST0026] **Do** use `also` for side effects that should not alter the chain — logging, validation, and debug inspection.
- [INST0027] **Do** use `run` on an object when you need to operate on it as `this` and return a computed result — choose `run` over `let` when the lambda accesses multiple members of the receiver.
- [INST0028] **Do** use `with` to call multiple functions on a non-nullable object when the return value of `with` itself is not needed — `with(config) { applyDefaults(); validate() }`.
- [INST0029] **Don't** nest scope functions — extract nested lambdas to named functions to preserve readability.

## Collections

- [INST0030] **Do** use read-only collection types (`List`, `Map`, `Set`) by default; use `MutableList`, `MutableMap`, `MutableSet` only when mutation is required.
- [INST0031] **Do** use `listOf`, `mapOf`, `setOf` to create immutable collections and `buildList`, `buildMap`, `buildSet` for complex construction.
- [INST0032] **Do** prefer collection operations (`map`, `filter`, `fold`, `groupBy`) over manual `for` loops when the intent is clearer.
- [INST0033] **Do** use `sequence { }` or `generateSequence` for lazy evaluation when working with large data sets or infinite streams.

## Coroutines

- [INST0034] **Do** use structured concurrency — launch coroutines within a `CoroutineScope` tied to a lifecycle; never use `GlobalScope` in production code.
- [INST0035] **Do** use `Flow` for reactive, asynchronous data streams; prefer cold `flow { }` over hot `StateFlow`/`SharedFlow` unless sharing across collectors is needed.
- [INST0036] **Do** use `withContext(Dispatchers.IO)` for blocking I/O and `withContext(Dispatchers.Default)` for CPU-intensive work — never block the calling dispatcher.
- [INST0037] **Do** use `supervisorScope` when child coroutine failures should not cancel siblings.
- [INST0038] **Don't** use `runBlocking` in production code — use it only in tests or at the top-level `main` function.

## Documentation

- [INST0039] **Do** add KDoc (`/** … */`) to all public and internal types, functions, and properties — focus on intent, parameters, return value, and thrown exceptions.
- [INST0040] **Don't** add KDoc that merely restates the function name — `/** Gets the name. */ fun getName()` adds no value.
