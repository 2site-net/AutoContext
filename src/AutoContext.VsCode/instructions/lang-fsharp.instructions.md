---
name: "lang-fsharp (v1.0.0)"
description: "Apply when writing or reviewing F# code (module layout, naming, type design, pattern matching, formatting, docs)."
applyTo: "**/*.{fs,fsi}"
---

# F# Coding Instructions

> These instructions target F# code — idiomatic style, type design, pattern matching, and formatting.

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Module & Namespace Layout

- [INST0001] **Do** use namespace declarations for library code and module declarations for application/script code — namespaces allow cross-file type extension; modules compile to static classes.
- [INST0002] **Do** place one primary type or module per file and name the file after that type or module.
- [INST0003] **Do** order declarations top-down in dependency order — the F# compiler requires definitions to appear before their first use within a file.
- [INST0004] **Do** use `[<AutoOpen>]` sparingly — only for modules that provide ubiquitous helpers truly needed everywhere; overuse pollutes the namespace.
- [INST0005] **Don't** nest modules more than one level deep — flatten the hierarchy or split into separate files for clarity.

### Naming

- [INST0006] **Do** use PascalCase for types, modules, union cases, record fields, and public functions — this is the standard .NET and F# convention.
- [INST0007] **Do** use camelCase for local bindings, parameters, and private functions.
- [INST0008] **Do** use PascalCase for abbreviations of three or more letters (e.g., `Xml`, `Http`) and uppercase for two-letter abbreviations (e.g., `IO`, `UI`).
- [INST0009] **Do** suffix F# module names with `Module` only when they share a name with a type in the same namespace — otherwise omit the suffix.
- [INST0010] **Don't** use Hungarian notation or type prefixes/suffixes on bindings (e.g., `strName`, `nameString`) — let the type system communicate types.

### Type Design

- [INST0011] **Do** prefer discriminated unions over class hierarchies for modelling closed sets of alternatives — they are exhaustively checked by the compiler.
- [INST0012] **Do** use record types for plain data with named fields — they provide structural equality, deconstruction, and `with` copy-and-update by default.
- [INST0013] **Do** use single-case discriminated unions for strongly-typed wrappers around primitive types (e.g., `type EmailAddress = EmailAddress of string`) — prevents accidental mixing of unrelated values.
- [INST0014] **Do** use `option` instead of `null` for absent values — pattern matching on `Some`/`None` eliminates null-reference errors at compile time.
- [INST0015] **Do** use `Result<'T, 'TError>` for operations that can fail with a meaningful error instead of throwing exceptions — reserve exceptions for truly unexpected failures.
- [INST0016] **Don't** define mutable record fields unless required for interop — prefer immutable records with `with` expressions.
- [INST0017] **Don't** use classes purely to hold data — use record types instead; classes are appropriate when you need encapsulation, interfaces, or OOP interop.

### Functions & Composition

- [INST0018] **Do** keep functions short and single-purpose — compose small functions with `>>` or pipe (`|>`) for larger workflows.
- [INST0019] **Do** use the pipe operator (`|>`) to make data flow left-to-right and keep the "subject" on the left — avoids deeply nested parentheses.
- [INST0020] **Do** prefer point-free composition (`>>`) only when it improves readability — introduce explicit parameters when the composition becomes unclear.
- [INST0021] **Do** use partial application to create reusable specialised functions from general ones.
- [INST0022] **Do** use `Async.Parallel`, `Async.Sequential`, or `task { }` (with `Task`-based code) for concurrent operations instead of sequential `let!` in a loop.
- [INST0023] **Don't** overuse point-free style — if the reader can't immediately tell what a composed pipeline does, name the intermediate steps or add parameters.
- [INST0024] **Don't** use mutable `let mutable` bindings as a default — prefer immutable bindings and recursive functions or folds for accumulation.

### Pattern Matching

- [INST0025] **Do** prefer `match` expressions over `if/elif/else` chains when branching on structured data — pattern matching is exhaustive and more readable.
- [INST0026] **Do** use active patterns (`(|Pattern|_|)`) to encapsulate complex matching logic that would otherwise clutter `match` expressions.
- [INST0027] **Do** handle all union cases explicitly rather than using a wildcard (`_`) catch-all — this ensures the compiler alerts you when new cases are added.
- [INST0028] **Do** use `function` (shorthand for `fun x -> match x with`) when a function's only purpose is pattern matching on its last argument.
- [INST0029] **Don't** nest `match` expressions more than one level deep — extract the inner match into a named function for clarity.

### Formatting & Whitespace

- [INST0030] **Do** indent with 4 spaces, no tabs — consistent with .NET conventions and `fantomas` defaults.
- [INST0031] **Do** insert a blank line between top-level `let` bindings, type definitions, and module declarations.
- [INST0032] **Do** insert a blank line before control flow expressions (`if`, `match`, `for`, `while`, `try`, `use`).
- [INST0033] **Do** insert a blank line between binding declarations and their first usage.
- [INST0034] **Do** align `|` in discriminated union and match cases at the same column.
- [INST0035] **Do** break long function signatures and pipelines across multiple lines — one pipeline stage per line aligned at the `|>` operator.
- [INST0036] **Don't** exceed a reasonable line length (ideally 100–120 characters) — break expressions at natural boundaries.

### Error Handling

- [INST0037] **Do** use `Result<'T, 'TError>` and railway-oriented programming for expected error paths — chain with `Result.bind`, `Result.map`, or a computation expression.
- [INST0038] **Do** use `try/with` only for unexpected exceptions or at system boundaries (I/O, interop) — not for normal control flow.
- [INST0039] **Do** define a domain-specific error discriminated union rather than returning raw strings or exception types as errors.
- [INST0040] **Don't** use `failwith` or `raise` for expected error conditions — they bypass the type system and force callers to catch exceptions.

### Documentation

- [INST0041] **Do** add XML doc comments (`/// <summary>`) to public types, modules, and functions — keep them brief and focused on intent.
- [INST0042] **Do** document discriminated union cases when their purpose isn't obvious from the case name alone.
- [INST0043] **Don't** add doc comments to private bindings unless the logic is genuinely non-obvious.
