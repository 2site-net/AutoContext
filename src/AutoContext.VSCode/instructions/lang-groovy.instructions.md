---
description: "Groovy coding style instructions: naming conventions, type safety, closures, GStrings, collections, error handling, Spock testing, and documentation."
applyTo: "**/*.{groovy,gvy}"
---
# Groovy Coding Style

## Naming

- [INST0001] **Do** use PascalCase for classes, traits, interfaces, and enums; use camelCase for methods, variables, and parameters.
- [INST0002] **Do** use SCREAMING_SNAKE_CASE for constants (`static final` fields).
- [INST0003] **Do** name packages in all-lowercase with dots as separators — follow the reversed-domain convention (e.g., `com.example.billing.api`).
- [INST0004] **Do** name boolean properties and methods with affirmative predicates — `isValid`, `hasAccess`, `canRetry` — not negations like `isNotEmpty`.
- [INST0005] **Don't** carry over type-prefix conventions from Java — write `def account = new Account()`, not `def objAccount` or `def strName`.

## Type Safety

- [INST0006] **Do** annotate production classes and methods with `@CompileStatic` — it eliminates dynamic dispatch overhead and catches type errors at compile time.
- [INST0007] **Do** use `@TypeChecked` when you need static type checking without the full restrictions of `@CompileStatic`.
- [INST0008] **Do** declare explicit types for method signatures, class fields, and public APIs — use `def` only for local variables where the type is obvious from context, or in scripts and DSLs where explicit typing would reduce clarity.
- [INST0009] **Do** use `@Immutable` for value objects — it generates `equals`, `hashCode`, and `toString` and makes all fields final.
- [INST0010] **Don't** rely on dynamic typing in production application code — dynamic dispatch is a powerful tool for scripting and metaprogramming, not for business logic.

## Language Features

- [INST0011] **Do** use closures in preference to anonymous inner classes — `list.each { item -> process(item) }` over anonymous `Closure` subclasses.
- [INST0012] **Do** use the safe-navigation operator (`?.`) to guard against `null` — `user?.address?.postcode` instead of explicit null checks.
- [INST0013] **Do** use the Elvis operator (`?:`) for null-coalescing and default values — `name ?: 'anonymous'`.
- [INST0014] **Do** use Groovy truth — empty collections, empty strings, `null`, and `0` are falsy; write `if (list)` instead of `if (!list.isEmpty())`.
- [INST0015] **Do** use the spread operator (`*.`) to invoke a method on every element — `teams*.captain` instead of `teams.collect { it.captain }`.
- [INST0016] **Do** use `@Delegate` to compose behaviour through delegation rather than inheritance.
- [INST0017] **Do** use traits for reusable behaviour — prefer `trait` over abstract classes when composing multiple capabilities.
- [INST0018] **Don't** override `toString`, `equals`, or `hashCode` manually in simple data classes — use `@ToString`, `@EqualsAndHashCode`, or `@Immutable` AST transforms instead.

## GStrings & Strings

- [INST0019] **Do** use double-quoted GStrings (`"Hello, ${name}!"`) for interpolation instead of string concatenation.
- [INST0020] **Do** use triple-double-quoted strings (`"""…"""`) for multiline string literals.
- [INST0021] **Do** use single-quoted strings (`'…'`) when the string contains no interpolation — it makes intent clear and avoids unintended evaluation.
- [INST0022] **Don't** pass a GString where a `String` is required in performance-sensitive code — call `.toString()` explicitly, since `GString` is a `CharSequence`, not a `String`.

## Collections

- [INST0023] **Do** use Groovy list (`[]`) and map (`[:]`) literals for concise collection construction.
- [INST0024] **Do** use GDK collection methods (`collect`, `findAll`, `find`, `inject`, `groupBy`, `countBy`, `flatten`) in preference to manual loops.
- [INST0025] **Do** use `collectEntries` to transform a list to a map — `items.collectEntries { [(it.id): it] }`.
- [INST0026] **Don't** modify a collection while iterating over it — use `findAll` or `removeIf` to filter, never `remove` inside `each`.

## Error Handling

- [INST0027] **Do** catch specific exception types rather than `Exception` or `Throwable` — let unexpected exceptions propagate to where they can be meaningfully handled.
- [INST0028] **Do** use multi-catch (`catch (IOException | SQLException e)`) to handle multiple exception types with the same recovery logic.
- [INST0029] **Do** use the `withCloseable` / `withStream` GDK pattern for any `Closeable` — `stream.withCloseable { … }` ensures the resource is always closed.
- [INST0030] **Don't** swallow exceptions silently — at a minimum, log the exception before recovering or rethrowing.

## Documentation

- [INST0031] **Do** add Groovydoc (`/** … */`) to all public and protected classes, methods, and fields — document intent, parameters (`@param`), return value (`@return`), and thrown exceptions (`@throws`).
- [INST0032] **Don't** add Groovydoc that merely restates the method name — `/** Gets the name. */ def getName()` adds no value.
