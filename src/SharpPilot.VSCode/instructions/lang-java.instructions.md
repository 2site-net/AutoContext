---
description: "Java coding style instructions: naming conventions, type design, error handling, streams, Optional, generics, concurrency, and documentation."
applyTo: "**/*.java"
---
# Java Coding Style

## Naming

- [INST0001] **Do** use PascalCase for classes, interfaces, enums, annotations, and records; use camelCase for methods, fields, parameters, and local variables.
- [INST0002] **Do** use UPPER_SNAKE_CASE for `static final` constants (e.g., `MAX_RETRY_COUNT`).
- [INST0003] **Do** name packages in all-lowercase with dots as separators — follow the reversed-domain convention (e.g., `com.example.billing.api`).
- [INST0004] **Don't** use Hungarian notation or type prefixes — write `Account account`, not `Account objAccount` or `IAccount`.
- [INST0005] **Do** name boolean methods and variables with affirmative predicates — `isValid`, `hasAccess`, `canRetry` — not negations like `isNotEmpty`.

## Type Design

- [INST0006] **Do** prefer composition over inheritance — favour delegating to a collaborator field instead of extending a concrete class.
- [INST0007] **Do** use records for immutable data carriers when the auto-generated `equals`/`hashCode`/`toString` suffice — avoid records when you need mutable state or custom identity semantics.
- [INST0008] **Do** declare classes, methods, and fields with the narrowest possible access modifier — `private` by default, widen only when required.
- [INST0009] **Do** program to interfaces, not implementations — declare fields, parameters, and return types as `List`, `Map`, `Set`, not `ArrayList`, `HashMap`, `HashSet`.
- [INST0010] **Don't** create marker interfaces or empty abstract classes for categorisation alone — use annotations or sealed interfaces.
- [INST0011] **Do** use sealed classes and interfaces (Java 17+) when the set of subtypes is fixed and known at compile time.

## Language Features

- [INST0012] **Do** use `var` for local variables when the type is obvious from the right-hand side (e.g., `var users = new ArrayList<User>()`) — use an explicit type when clarity requires it.
- [INST0013] **Do** use pattern matching for `instanceof` (Java 16+) — `if (shape instanceof Circle c)` — instead of explicit cast after check.
- [INST0014] **Do** use switch expressions (Java 14+) instead of switch statements when producing a result — exhaustive matching with `->` arms is cleaner.
- [INST0015] **Do** use text blocks (`"""`) for multi-line strings (Java 15+) — SQL, JSON templates, and similar literals are easier to read.
- [INST0016] **Don't** use raw types — always parameterise generic types (`List<String>`, not `List`).

## Null Handling & Optional

- [INST0017] **Do** return `Optional<T>` from methods that may legitimately have no result — never return `null` from such methods.
- [INST0018] **Don't** use `Optional` as a field type, constructor parameter, or method parameter — it is designed for return types only.
- [INST0019] **Do** use `Optional.map`, `Optional.flatMap`, and `Optional.orElseGet` instead of `isPresent`/`get` chains.
- [INST0020] **Don't** call `Optional.get()` without first confirming presence — prefer `orElseThrow()` with a meaningful exception.

## Error Handling

- [INST0021] **Do** throw specific, meaningful exception types — `IllegalArgumentException`, `IllegalStateException`, domain-specific exceptions — not generic `Exception` or `RuntimeException`.
- [INST0022] **Do** use try-with-resources for all `AutoCloseable` resources — never rely on `finally` blocks for cleanup when try-with-resources is applicable.
- [INST0023] **Don't** catch `Exception` or `Throwable` as a blanket handler — catch the narrowest type possible and let unexpected failures propagate.
- [INST0024] **Don't** use exceptions for control flow — use conditional checks or `Optional` instead.
- [INST0025] **Do** include the original exception as the cause when wrapping — `throw new ServiceException("msg", original)`.

## Streams & Collections

- [INST0026] **Do** prefer `Stream` pipelines for transformations, filtering, and aggregation over manual `for` loops when the intent is clearer.
- [INST0027] **Don't** write side-effecting lambdas inside `map`, `filter`, or `flatMap` — use `forEach` or `peek` (for debugging only) if a side effect is needed.
- [INST0028] **Do** use `Stream.toList()` (Java 16+), `Collectors.toUnmodifiableList()`, `List.of()`, `Map.of()`, or `Collections.unmodifiable*` to return immutable collections from public APIs.
- [INST0029] **Don't** nest more than two or three stream operations before extracting to a named method — readability matters more than a single pipeline.

## Concurrency

- [INST0030] **Do** use `ExecutorService` and `CompletableFuture` (or virtual threads on Java 21+) instead of manual `Thread` management.
- [INST0031] **Do** use `java.util.concurrent` structures (`ConcurrentHashMap`, `AtomicReference`, `CountDownLatch`) over manual `synchronized` blocks when possible.
- [INST0032] **Don't** hold locks while performing I/O or calling external services — minimise critical sections.

## Generics

- [INST0033] **Do** use bounded type parameters (`<T extends Comparable<T>>`) to express constraints instead of casting at runtime.
- [INST0034] **Do** apply PECS (Producer-Extends, Consumer-Super) — use `? extends T` for read-only parameters and `? super T` for write-only parameters.

## Documentation

- [INST0035] **Do** add Javadoc (`/** … */`) to all public and protected types, methods, and constructors — focus on intent, parameters, return value, and thrown exceptions.
- [INST0036] **Don't** add Javadoc that merely restates the method name — `/** Gets the name. */ getName()` adds no value.

## Formatting

- [INST0037] **Do** place opening braces on the same line as the declaration — follow the "K&R" / Java standard style.
- [INST0038] **Do** always use braces for `if`, `for`, `while`, and `do` bodies — even single-line statements — except single-line guard clauses (early `return`/`throw`).
- [INST0039] **Do** group related statements into logical paragraphs separated by blank lines and insert a blank line before control flow statements.
