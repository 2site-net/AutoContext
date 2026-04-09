---
description: "Dart coding style instructions: naming conventions, null safety, type design, async patterns, collections, error handling, and documentation."
applyTo: "**/*.dart"
---
# Dart Coding Style

## Naming

- [INST0001] **Do** use `lowerCamelCase` for variables, functions, parameters, and named arguments; use `UpperCamelCase` for classes, enums, type aliases, extensions, and mixins.
- [INST0002] **Do** use `lowerCamelCase` for constant names — `const defaultTimeout = Duration(seconds: 30)` — not `SCREAMING_SNAKE_CASE`.
- [INST0003] **Do** use `lowercase_with_underscores` for library, package, directory, and source file names.
- [INST0004] **Do** name boolean variables and getters with affirmative predicates — `isValid`, `hasAccess`, `canRetry` — not negations like `isNotEmpty`.
- [INST0005] **Do** prefix private members with an underscore (`_count`, `_fetchData()`) — Dart uses the underscore as a library-level visibility modifier.
- [INST0006] **Don't** use Hungarian notation or type prefixes — write `account`, not `objAccount` or `strName`.

## Null Safety

- [INST0007] **Do** design APIs to be non-nullable by default — use `T?` only when `null` is a semantically valid value.
- [INST0008] **Do** use null-aware operators (`?.`, `??`, `??=`, `?..`) to handle nullable types concisely instead of explicit null checks.
- [INST0009] **Do** use `late` only when a non-nullable field is guaranteed to be initialised before first access — prefer constructor initialisation or nullable types with null checks when the guarantee is not self-evident.
- [INST0010] **Don't** use the null assertion operator (`!`) except at system boundaries where a null value is genuinely impossible — each use is a potential runtime exception.

## Type Design

- [INST0011] **Do** rely on type inference for local variables — write `final items = <String>[]` instead of `final List<String> items = <String>[]`.
- [INST0012] **Do** annotate types on public APIs — function return types, parameters, and public fields should have explicit type annotations for readability and documentation.
- [INST0013] **Do** prefer `final` for local variables that are not reassigned — it communicates intent and prevents accidental mutation.
- [INST0014] **Do** use `sealed class` to model closed type hierarchies — enables exhaustive `switch` expressions and pattern matching.
- [INST0015] **Do** use `enum` for fixed sets of constant values; add members and methods via enhanced enums when behaviour varies per value.
- [INST0016] **Do** use `typedef` for function signatures that are reused across the codebase — `typedef JsonMap = Map<String, dynamic>`.
- [INST0017] **Don't** use `dynamic` unless interfacing with untyped data (JSON, interop) — it disables static analysis and autocompletion.

## Immutability & Records

- [INST0018] **Do** use `const` constructors for classes whose instances are compile-time constants — widgets, configuration objects, and value types benefit from canonicalisation.
- [INST0019] **Do** use records (`(String, int)` or `({String name, int age})`) for lightweight groupings of values that don't need methods or identity.
- [INST0020] **Do** use `unmodifiable` views (`List.unmodifiable`, `Map.unmodifiable`) when exposing internal collections through public APIs.

## Async & Concurrency

- [INST0021] **Do** use `async`/`await` for all asynchronous control flow — it reads sequentially and handles errors naturally with `try`/`catch`.
- [INST0022] **Do** return `Future<void>` (not `void`) from async functions so callers can await or handle errors.
- [INST0023] **Do** use `Stream` for event-based or multi-value asynchronous data — prefer `Stream.fromIterable`, `StreamController`, or `async*` generators.
- [INST0024] **Do** cancel stream subscriptions in `dispose()` methods to prevent memory leaks — store the `StreamSubscription` and call `cancel()`.
- [INST0025] **Do** use `Future.wait` for concurrent independent futures and `Completer` when bridging callback-based APIs into future-based ones.
- [INST0026] **Don't** use `Isolate.spawn` for trivial tasks — use `compute()` (Flutter) or `Isolate.run()` for CPU-bound work that justifies the isolation overhead.

## Collections

- [INST0027] **Do** use collection literals (`[]`, `{}`, `<K, V>{}`) instead of constructor calls — `final names = <String>[]` not `final names = List<String>()`.
- [INST0028] **Do** use collection-if, collection-for, and spread operators (`...`, `...?`) to build collections declaratively.
- [INST0029] **Do** use `Iterable` methods (`map`, `where`, `fold`, `expand`, `any`, `every`) over manual `for` loops when the intent is clearer.
- [INST0030] **Don't** call `.toList()` on an `Iterable` unless the consumer actually needs a `List` — keep it lazy when possible.

## Error Handling

- [INST0031] **Do** throw specific exception types — define custom exceptions that `implement Exception` with a descriptive message.
- [INST0032] **Do** use `try`/`on ExceptionType catch (e)` to handle specific exception types — avoid bare `catch (e)` that swallows all errors indiscriminately.
- [INST0033] **Do** use `rethrow` instead of `throw e` to preserve the original stack trace.
- [INST0034] **Don't** use exceptions for expected control flow — use `Result`-like patterns or nullable returns for operations that routinely fail (parsing, lookups).

## Pattern Matching

- [INST0035] **Do** use `switch` expressions for exhaustive matching on sealed types, enums, and records — they are expressions that return values.
- [INST0036] **Do** use destructuring patterns in `switch` cases, `if-case`, and variable declarations to extract values concisely.
- [INST0037] **Do** use guard clauses (`when`) in `switch` cases to add conditions without nesting — `case Rectangle(width: var w) when w > 0`.

## Extensions & Mixins

- [INST0038] **Do** use extension methods to add behaviour to types you don't own — prefer them over top-level utility functions.
- [INST0039] **Do** use `mixin` for reusable behaviour that applies across unrelated class hierarchies — prefer `mixin` over abstract classes when there is no shared state contract.
- [INST0040] **Do** name extensions descriptively — `extension StringValidation on String` — unless the extension is private, in which case it can be unnamed.

## Documentation

- [INST0041] **Do** use `///` doc comments on all public types, functions, and properties — write a one-sentence summary followed by details if needed.
- [INST0042] **Don't** write doc comments that restate the declaration — `/// Gets the name.` on `String get name` adds no value.
- [INST0043] **Do** use square brackets in doc comments to reference other symbols — `/// Returns the [Widget] for the given [BuildContext].`

## Formatting

- [INST0044] **Do** format all code with `dart format` — the official formatter enforces consistent style across the codebase.
- [INST0045] **Do** use trailing commas on the last argument or element in multi-line function calls, parameter lists, collections, and widget trees — it produces cleaner diffs and lets `dart format` break lines predictably.
