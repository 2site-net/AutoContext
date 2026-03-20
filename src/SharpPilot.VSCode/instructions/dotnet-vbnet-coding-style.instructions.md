---
description: "VB.NET code style rules: naming, module layout, type design, error handling, formatting, and documentation conventions."
applyTo: "**/*.vb"
---
# VB.NET Coding Style

> These rules target VB.NET code — idiomatic style, naming, type design, error handling, and formatting.

## Naming

- **Do** use PascalCase for types, namespaces, methods, properties, events, enums, and public fields.
- **Do** use camelCase for local variables and parameters.
- **Do** prefix interface names with `I` (e.g., `IDisposable`, `IComparable`).
- **Do** use PascalCase for constants and enum members — VB.NET convention does not use `ALL_CAPS`.
- **Do** suffix event handlers with `EventHandler` and event arguments with `EventArgs`.
- **Don't** use Hungarian notation or type prefixes/suffixes on variables (e.g., `strName`, `intCount`).
- **Don't** use underscores in identifiers except for `_` prefix on private fields (e.g., `_connectionString`).

## Module & Namespace Layout

- **Do** use one primary type per file and name the file after that type.
- **Do** organize files into folders matching the namespace hierarchy.
- **Do** place `Imports` statements at the top of the file, before the `Namespace` declaration.
- **Do** order `Imports` alphabetically, with `System` namespaces first.
- **Do** use `Namespace` blocks rather than file-scoped namespaces (VB.NET does not support file-scoped namespaces).
- **Don't** use `Module` for utility code when a `Class` with `Shared` members would be clearer — reserve `Module` for extension methods and truly global helpers.

## Type Design

- **Do** prefer `Class` over `Module` unless you specifically need extension methods or a global scope.
- **Do** use `Structure` for small, immutable value types with value semantics.
- **Do** implement `IDisposable` with the full dispose pattern (`Dispose(disposing As Boolean)`) when holding unmanaged resources.
- **Do** use `Enum` for named sets of related constants — always specify the underlying type if it matters for interop.
- **Do** seal classes with `NotInheritable` when inheritance is not intended.
- **Don't** use `Object` as a catch-all parameter or return type — use generics or specific types for type safety.
- **Don't** expose public fields — use properties with `Property` syntax instead.

## Properties & Fields

- **Do** use auto-implemented properties for simple get/set scenarios.
- **Do** use `ReadOnly` properties for values that should not change after construction.
- **Do** use `Private` or `Friend` access for fields; expose data through properties.
- **Don't** use public instance fields — use `Property` definitions to maintain encapsulation.

## Methods & Functions

- **Do** use `Function` when the method returns a value and `Sub` when it does not.
- **Do** keep methods short and focused on a single responsibility.
- **Do** use `Optional` parameters sparingly — prefer overloads when the combinations are few and distinct.
- **Do** use `ByVal` semantics (the default) unless you specifically need `ByRef` for out-parameter scenarios.
- **Don't** use `GoTo` — use structured control flow (`For`, `While`, `Do`, `Select Case`, `Try`/`Catch`) instead.

## Error Handling

- **Do** use `Try`/`Catch`/`Finally` for exception handling — never use `On Error` or `Resume`.
- **Do** catch specific exception types rather than `Exception` — catch the most derived type that makes sense.
- **Do** use `Throw` (without an argument) to re-throw the current exception preserving the stack trace — never use `Throw ex`.
- **Do** use `Using` blocks for `IDisposable` objects to guarantee cleanup.
- **Don't** swallow exceptions silently — at minimum, log the error.
- **Don't** use exceptions for normal control flow — check preconditions instead.

## Strings & Conversions

- **Do** use string interpolation (`$"Hello {name}"`) instead of `String.Format` or concatenation for readability.
- **Do** use `String.Equals` with `StringComparison` for culture-aware or case-insensitive comparisons.
- **Do** use `TryCast` instead of `DirectCast` when the cast might fail — check for `Nothing` afterward.
- **Do** use `.ToString(CultureInfo.InvariantCulture)` for numeric-to-string conversions in serialization or logging.
- **Don't** use legacy VB conversion functions (`CStr`, `CInt`, `Val`) — use `Convert`, `TryParse`, or `CType` for clarity and type safety.
- **Don't** rely on implicit conversions with `Option Strict Off` — always use `Option Strict On`.

## Option Statements

- **Do** enable `Option Strict On` — this enforces explicit type conversions and catches type errors at compile time.
- **Do** enable `Option Explicit On` — this requires all variables to be declared before use.
- **Do** enable `Option Infer On` — this allows the compiler to infer local variable types from their initializers while still enforcing `Option Strict`.
- **Don't** use `Option Compare Text` unless you specifically need case-insensitive string comparison by default.

## Control Flow

- **Do** prefer `Select Case` over long `If`/`ElseIf` chains when branching on a single value.
- **Do** use `For Each` for iterating collections instead of index-based `For` loops when the index is not needed.
- **Do** use `AndAlso` and `OrElse` (short-circuit operators) instead of `And` and `Or` for boolean conditions.
- **Don't** nest control structures more than three levels deep — extract inner logic into separate methods.

## LINQ

- **Do** prefer LINQ query syntax or method syntax consistently within a project — do not mix styles arbitrarily.
- **Do** use meaningful range variable names in LINQ queries (e.g., `From order In orders` not `From x In orders`).
- **Do** prefer `Enumerable` methods (`.Where()`, `.Select()`, `.FirstOrDefault()`) for simple transformations.
- **Don't** use LINQ for performance-critical tight loops where a plain `For` loop would be more efficient.

## Async/Await

- **Do** use `Async Function` / `Await` for asynchronous operations — follow the same .NET async conventions as C#.
- **Do** suffix asynchronous methods with `Async` (e.g., `GetDataAsync`).
- **Do** return `Task` from `Async Sub` only for event handlers — all other async methods should return `Task` or `Task(Of T)`.
- **Don't** use `Task.Result` or `Task.Wait()` — they block the calling thread and can cause deadlocks.

## Formatting & Whitespace

- **Do** indent with 4 spaces, no tabs.
- **Do** place one statement per line — do not use the colon (`:`) to combine multiple statements on one line.
- **Do** use line continuation (`_`) or implicit line continuation (after operators, commas, parentheses) to break long lines.
- **Do** insert blank lines between method definitions, property definitions, and logical sections within a method.
- **Don't** exceed a reasonable line length (ideally 100–120 characters) — break at natural boundaries.

## XML Comments

- **Do** add XML doc comments (`'''`) to public types, methods, and properties — describe purpose, parameters, return values, and exceptions.
- **Do** use `<summary>`, `<param>`, `<returns>`, and `<exception>` tags.
- **Don't** add XML doc comments to private members unless the logic is genuinely non-obvious.
