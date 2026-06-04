---
name: "lang-ruby (v1.0.0)"
description: "Apply when writing or reviewing Ruby code (naming, blocks, error handling, classes, modules, collections, metaprogramming)."
applyTo: "**/*.rb"
---

# Ruby Coding Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Naming

- [INST0001] **Do** use `snake_case` for methods, variables, and file names; `PascalCase` for classes and modules; `SCREAMING_SNAKE_CASE` for constants.
- [INST0002] **Do** use predicate-style names ending in `?` for methods that return a boolean — `empty?`, `valid?`, `include?`.
- [INST0003] **Do** use `!` suffix (bang) for methods that mutate the receiver or raise instead of returning `nil` — pair them with a non-bang counterpart when both variants exist.
- [INST0004] **Don't** prefix boolean variables with `is_` or `has_` — Ruby's `?` convention on methods already signals the return type; for variables prefer positive names like `valid`, `admin`.

### Blocks, Procs & Lambdas

- [INST0005] **Do** use `{}` for single-line blocks and `do...end` for multi-line blocks — this is the community convention and aids readability.
- [INST0006] **Do** prefer `&:method_name` shorthand when a block only calls a single method — `names.map(&:upcase)` instead of `names.map { |n| n.upcase }`.
- [INST0007] **Do** prefer lambdas over procs when you need strict arity checking and `return` that exits only the lambda — use `->` (stabby lambda) syntax for conciseness.
- [INST0008] **Don't** use `Proc.new` or `proc {}` when a lambda is more appropriate — procs have surprising `return` semantics that can exit the enclosing method.

### Classes & Modules

- [INST0009] **Do** use `attr_reader`, `attr_writer`, or `attr_accessor` instead of writing getters and setters manually — they are concise and idiomatic.
- [INST0010] **Do** use modules for namespacing and for sharing behavior via `include` or `extend` — prefer composition over deep class hierarchies.
- [INST0011] **Do** use `Struct` or `Data` (Ruby 3.2+) for simple value objects — they provide equality, hashing, and attribute accessors for free.
- [INST0012] **Do** keep `initialize` focused on assigning instance variables — move validation or derived computation to named class methods or builder patterns.
- [INST0013] **Don't** use class variables (`@@var`) — they are shared across the entire inheritance hierarchy and cause subtle bugs; use class-level instance variables (`@var` inside `class << self`) instead.
- [INST0014] **Don't** reopen core classes (monkey-patching) unless absolutely necessary — prefer Refinements or wrapper modules to limit the scope of changes.

### Error Handling

- [INST0015] **Do** rescue specific exception classes — `rescue ArgumentError, IOError` — never use bare `rescue` or `rescue Exception`, which catches signals and system errors.
- [INST0016] **Do** define custom exception classes inheriting from `StandardError` for domain-specific errors — include a meaningful message and optional context attributes.
- [INST0017] **Do** use `ensure` for cleanup that must run regardless of success or failure — prefer it over `rescue` for resource release.
- [INST0018] **Don't** use exceptions for control flow — they are expensive; use return values, `throw`/`catch`, or guard clauses for expected conditions.

### Collections & Enumerable

- [INST0019] **Do** use `Enumerable` methods (`map`, `select`, `reject`, `reduce`, `flat_map`, `each_with_object`) over manual loops — they are expressive and compose well.
- [INST0020] **Do** use `Hash#fetch` with a default or block instead of `Hash#[]` when the key might be missing — it makes the failure mode explicit.
- [INST0021] **Do** use `freeze` on constant collections and strings to prevent accidental mutation — combine with `# frozen_string_literal: true` magic comment at the top of every file.
- [INST0022] **Don't** mutate a collection while iterating over it — build a new collection or use `reject!`/`select!` which are designed for in-place filtering.

### Methods & Control Flow

- [INST0023] **Do** rely on implicit returns — the last evaluated expression is the return value; use explicit `return` only for early exits.
- [INST0024] **Do** use guard clauses (`return unless`, `return if`, `raise unless`) at the top of methods to handle edge cases early and keep the happy path unindented.
- [INST0025] **Do** use `case/in` pattern matching (Ruby 3.0+) for destructuring complex data — it is clearer than nested `if`/`elsif` chains.
- [INST0026] **Don't** use `and`/`or` for boolean logic — they have lower precedence than `&&`/`||` and can produce surprising results; reserve `and`/`or` for control flow if at all.

### Strings & Symbols

- [INST0027] **Do** use double-quoted strings only when interpolation or escape sequences are needed — use single-quoted strings for plain literals.
- [INST0028] **Do** use symbols (`:name`) for identifiers, hash keys, and enum-like values — they are immutable, interned, and faster to compare than strings.
- [INST0029] **Do** use heredocs (`<<~HEREDOC`) with squiggly syntax for multi-line strings — it strips leading indentation and keeps code readable.

### Metaprogramming

- [INST0030] **Do** prefer `define_method` over `method_missing` when the set of dynamic methods is known — it is explicit and shows up in method listings.
- [INST0031] **Do** always define `respond_to_missing?` when overriding `method_missing` — without it, `respond_to?` returns incorrect results.
- [INST0032] **Don't** use `eval`, `class_eval` with string arguments, or `send` with user-supplied input — they are security risks; use block forms of `class_eval`/`instance_eval` or public APIs instead.

### Testing Style

- [INST0033] **Do** follow the Arrange-Act-Assert pattern — separate setup, action, and verification with blank lines for clarity.
- [INST0034] **Do** use `freeze_time` or `travel_to` for time-dependent tests — never depend on `Time.now` in assertions.
- [INST0035] **Don't** test private methods directly — test the public interface; if a private method is complex enough to warrant its own tests, it should be extracted into its own class.

### Documentation

- [INST0036] **Do** use YARD-style doc comments (`# @param`, `# @return`, `# @raise`) for all public methods — include a one-line summary, parameter types, return type, and raised exceptions.
- [INST0037] **Don't** restate what the code already says — focus comments on *why* a decision was made, edge cases, and non-obvious behaviour.

### Formatting

- [INST0038] **Do** add `# frozen_string_literal: true` as the first line of every Ruby source file — it prevents accidental string mutation and improves performance.
- [INST0039] **Do** use 2-space indentation — this is the universal Ruby community standard.
