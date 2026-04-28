---
name: "lang-vbnet (v1.0.0)"
description: "VB.NET code style instructions: naming, module layout, type design, error handling, formatting, and documentation conventions."
applyTo: "**/*.vb"
---

# VB.NET Coding Instructions

> These instructions target VB.NET code — idiomatic style, naming, type design, error handling, and formatting.

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Naming

- [INST0001] **Do** use PascalCase for types, namespaces, methods, properties, events, enums, and public fields.
- [INST0002] **Do** use camelCase for local variables and parameters.
- [INST0003] **Do** prefix interface names with `I` (e.g., `IDisposable`, `IComparable`).
- [INST0004] **Do** use PascalCase for constants and enum members — VB.NET convention does not use `ALL_CAPS`.
- [INST0005] **Do** suffix event handlers with `EventHandler` and event arguments with `EventArgs`.
- [INST0006] **Don't** use Hungarian notation or type prefixes/suffixes on variables (e.g., `strName`, `intCount`).
- [INST0007] **Don't** use underscores in identifiers except for `_` prefix on private fields (e.g., `_connectionString`).

### Module & Namespace Layout

- [INST0008] **Do** use one primary type per file and name the file after that type.
- [INST0009] **Do** organize files into folders matching the namespace hierarchy.
- [INST0010] **Do** place `Imports` statements at the top of the file, before the `Namespace` declaration.
- [INST0011] **Do** order `Imports` alphabetically, with `System` namespaces first.
- [INST0012] **Do** use `Namespace` blocks rather than file-scoped namespaces (VB.NET does not support file-scoped namespaces).
- [INST0013] **Don't** use `Module` for utility code when a `Class` with `Shared` members would be clearer — reserve `Module` for extension methods and truly global helpers.

### Type Design

- [INST0014] **Do** prefer `Class` over `Module` unless you specifically need extension methods or a global scope.
- [INST0015] **Do** use `Structure` for small, immutable value types with value semantics.
- [INST0016] **Do** implement `IDisposable` with the full dispose pattern (`Dispose(disposing As Boolean)`) when holding unmanaged resources.
- [INST0017] **Do** use `Enum` for named sets of related constants — always specify the underlying type if it matters for interop.
- [INST0018] **Do** seal classes with `NotInheritable` when inheritance is not intended.
- [INST0019] **Don't** use `Object` as a catch-all parameter or return type — use generics or specific types for type safety.
- [INST0020] **Don't** expose public fields — use properties with `Property` syntax instead.

### Properties & Fields

- [INST0021] **Do** use auto-implemented properties for simple get/set scenarios.
- [INST0022] **Do** use `ReadOnly` properties for values that should not change after construction.
- [INST0023] **Do** use `Private` or `Friend` access for fields; expose data through properties.
- [INST0024] **Don't** use public instance fields — use `Property` definitions to maintain encapsulation.

### Methods & Functions

- [INST0025] **Do** use `Function` when the method returns a value and `Sub` when it does not.
- [INST0026] **Do** keep methods short and focused on a single responsibility.
- [INST0027] **Do** use `Optional` parameters sparingly — prefer overloads when the combinations are few and distinct.
- [INST0028] **Do** use `ByVal` semantics (the default) unless you specifically need `ByRef` for out-parameter scenarios.
- [INST0029] **Don't** use `GoTo` — use structured control flow (`For`, `While`, `Do`, `Select Case`, `Try`/`Catch`) instead.

### Error Handling

- [INST0030] **Do** use `Try`/`Catch`/`Finally` for exception handling — never use `On Error` or `Resume`.
- [INST0031] **Do** catch specific exception types rather than `Exception` — catch the most derived type that makes sense.
- [INST0032] **Do** use `Throw` (without an argument) to re-throw the current exception preserving the stack trace — never use `Throw ex`.
- [INST0033] **Do** use `Using` blocks for `IDisposable` objects to guarantee cleanup.
- [INST0034] **Don't** swallow exceptions silently — at minimum, log the error.
- [INST0035] **Don't** use exceptions for normal control flow — check preconditions instead.

### Strings & Conversions

- [INST0036] **Do** use string interpolation (`$"Hello {name}"`) instead of `String.Format` or concatenation for readability.
- [INST0037] **Do** use `String.Equals` with `StringComparison` for culture-aware or case-insensitive comparisons.
- [INST0038] **Do** use `TryCast` instead of `DirectCast` when the cast might fail — check for `Nothing` afterward.
- [INST0039] **Do** use `.ToString(CultureInfo.InvariantCulture)` for numeric-to-string conversions in serialization or logging.
- [INST0040] **Don't** use legacy VB conversion functions (`CStr`, `CInt`, `Val`) — use `Convert`, `TryParse`, or `CType` for clarity and type safety.
- [INST0041] **Don't** rely on implicit conversions with `Option Strict Off` — always use `Option Strict On`.

### Option Statements

- [INST0042] **Do** enable `Option Strict On` — this enforces explicit type conversions and catches type errors at compile time.
- [INST0043] **Do** enable `Option Explicit On` — this requires all variables to be declared before use.
- [INST0044] **Do** enable `Option Infer On` — this allows the compiler to infer local variable types from their initializers while still enforcing `Option Strict`.
- [INST0045] **Don't** use `Option Compare Text` unless you specifically need case-insensitive string comparison by default.

### Control Flow

- [INST0046] **Do** prefer `Select Case` over long `If`/`ElseIf` chains when branching on a single value.
- [INST0047] **Do** use `For Each` for iterating collections instead of index-based `For` loops when the index is not needed.
- [INST0048] **Do** use `AndAlso` and `OrElse` (short-circuit operators) instead of `And` and `Or` for boolean conditions.
- [INST0049] **Don't** nest control structures more than three levels deep — extract inner logic into separate methods.

### LINQ

- [INST0050] **Do** prefer LINQ query syntax or method syntax consistently within a project — do not mix styles arbitrarily.
- [INST0051] **Do** use meaningful range variable names in LINQ queries (e.g., `From order In orders` not `From x In orders`).
- [INST0052] **Do** prefer `Enumerable` methods (`.Where()`, `.Select()`, `.FirstOrDefault()`) for simple transformations.
- [INST0053] **Don't** use LINQ for performance-critical tight loops where a plain `For` loop would be more efficient.

### Async/Await

- [INST0054] **Do** use `Async Function` / `Await` for asynchronous operations — follow the same .NET async conventions as C#.
- [INST0055] **Do** suffix asynchronous methods with `Async` (e.g., `GetDataAsync`).
- [INST0056] **Do** return `Task` from `Async Sub` only for event handlers — all other async methods should return `Task` or `Task(Of T)`.
- [INST0057] **Don't** use `Task.Result` or `Task.Wait()` — they block the calling thread and can cause deadlocks.

### Formatting & Whitespace

- [INST0058] **Do** indent with 4 spaces, no tabs.
- [INST0059] **Do** place one statement per line — do not use the colon (`:`) to combine multiple statements on one line.
- [INST0060] **Do** use line continuation (`_`) or implicit line continuation (after operators, commas, parentheses) to break long lines.
- [INST0061] **Do** insert blank lines between method definitions, property definitions, and logical sections within a method.
- [INST0062] **Don't** exceed a reasonable line length (ideally 100–120 characters) — break at natural boundaries.

### XML Comments

- [INST0063] **Do** add XML doc comments (`'''`) to public types, methods, and properties — describe purpose, parameters, return values, and exceptions.
- [INST0064] **Do** use `<summary>`, `<param>`, `<returns>`, and `<exception>` tags.
- [INST0065] **Don't** add XML doc comments to private members unless the logic is genuinely non-obvious.
