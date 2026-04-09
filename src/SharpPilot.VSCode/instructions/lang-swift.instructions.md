---
description: "Swift coding style instructions: naming conventions, optionals, value types, protocols, concurrency, error handling, collections, and documentation."
applyTo: "**/*.swift"
---
# Swift Coding Style

## Naming

- [INST0001] **Do** use `lowerCamelCase` for functions, methods, variables, and parameters; `UpperCamelCase` for types (structs, classes, enums, protocols, typealiases); `lowerCamelCase` for enum cases.
- [INST0002] **Do** name protocols after the capability they describe — use `-able`, `-ible`, or `-ing` suffixes for capability protocols (`Equatable`, `Codable`) and noun phrases for role protocols (`Collection`, `Delegate`).
- [INST0003] **Do** name factory methods with `make` prefix (`makeIterator()`) and conversion methods that return a new type with `init` or a descriptive name — avoid `to`/`as` prefixes for non-cast conversions.
- [INST0004] **Don't** use Hungarian notation or type-encoding prefixes — write `let name: String`, not `let strName: String`; Swift's type system makes such prefixes redundant.

## Optionals

- [INST0005] **Do** use optional binding (`if let`, `guard let`) to safely unwrap optionals — prefer `guard let` for early exits, `if let` for conditional logic.
- [INST0006] **Do** use `guard let` at the top of functions to validate preconditions and unwrap optionals — it keeps the happy path unindented and makes exit conditions explicit.
- [INST0007] **Do** use nil-coalescing (`??`) to provide sensible defaults and optional chaining (`?.`) to access properties on optional values — they reduce nesting.
- [INST0008] **Don't** force-unwrap (`!`) optionals except when `nil` would indicate a programmer error (e.g., `IBOutlet` connections, resources bundled with the app) — force unwraps crash at runtime.
- [INST0009] **Don't** use implicitly unwrapped optionals (`Type!`) outside of `@IBOutlet` and framework-required contexts — they bypass the compiler's nil safety checks.

## Value Types & Mutability

- [INST0010] **Do** prefer structs over classes for data types that do not require reference semantics or inheritance — structs provide value semantics and are stack-allocated when possible.
- [INST0011] **Do** mark properties and methods as immutable by default — use `let` over `var`, and only add `mutating` when the method genuinely modifies state.
- [INST0012] **Do** use `Sendable` structs for data shared across concurrency boundaries — value types with immutable properties are `Sendable` by default.
- [INST0013] **Don't** use classes solely for grouping related functions — use an `enum` with no cases as a namespace, or use free functions within a module.

## Type Design

- [INST0014] **Do** use enums with associated values to model finite sets of domain alternatives — `enum Result { case success(Value), failure(Error) }` — instead of status codes or sentinel values.
- [INST0015] **Do** use `typealias` to clarify the intent of complex generic types — `typealias JSON = [String: Any]` — but avoid aliases that merely rename simple types.
- [INST0016] **Do** use access control deliberately — default to the minimum visibility needed (`private`, `fileprivate`, `internal`) and only expose `public` or `open` symbols that form the module's API.
- [INST0017] **Don't** make classes `open` unless you intentionally design them for subclassing — prefer `final class` or struct-based composition.

## Protocols & Extensions

- [INST0018] **Do** use protocol-oriented design — define small, focused protocols and compose them rather than building deep class hierarchies.
- [INST0019] **Do** provide default implementations in protocol extensions for shared behavior — conforming types opt in to the default or provide their own.
- [INST0020] **Do** use extensions to organise conformances — one extension per protocol conformance makes the grouping clear and improves readability.
- [INST0021] **Don't** add stored properties in extensions — use computed properties or associated objects only when necessary, and prefer refactoring the type instead.

## Concurrency (Swift Concurrency)

- [INST0022] **Do** use `async`/`await` for asynchronous code — it is safer and more readable than completion-handler-based patterns or Combine chains.
- [INST0023] **Do** use structured concurrency (`async let`, `TaskGroup`) to manage concurrent work — it automatically handles cancellation and error propagation.
- [INST0024] **Do** annotate types that protect mutable state with `@MainActor` or a custom global actor — prefer actor isolation over manual locking.
- [INST0025] **Do** use `Actor` types to encapsulate mutable shared state — actors serialize access and prevent data races at compile time.
- [INST0026] **Don't** use `Task.detached` unless you specifically need to escape the current actor context — prefer `Task { }` to inherit the caller's actor and priority.
- [INST0027] **Don't** block the main actor with synchronous work — offload heavy computation to a detached task or a background executor.

## Error Handling

- [INST0028] **Do** use Swift's `throw`/`catch` mechanism for recoverable errors — define domain-specific `Error`-conforming enums with associated values for context.
- [INST0029] **Do** use `try?` when the error can be safely discarded and a `nil` result is sufficient — use `try!` only when failure is impossible (e.g., compile-time-known valid regex).
- [INST0030] **Do** use `Result<Success, Failure>` when errors must be stored, returned asynchronously, or passed through non-throwing APIs.
- [INST0031] **Don't** use `fatalError()` or `preconditionFailure()` for expected runtime conditions — reserve them for truly unrecoverable programmer errors.

## Collections & Functional Patterns

- [INST0032] **Do** use `map`, `filter`, `compactMap`, `flatMap`, `reduce` for transforming collections — they express intent clearly and compose well.
- [INST0033] **Do** use `lazy` sequences for chained transformations on large collections — `collection.lazy.filter { }.map { }` avoids intermediate allocations.
- [INST0034] **Do** prefer `for-in` with `where` clauses over `filter` followed by `forEach` — `for item in list where item.isValid { }` reads naturally and avoids closures.
- [INST0035] **Don't** use `forEach` with side effects that depend on control flow (`return` inside `forEach` exits only the closure, not the enclosing function) — use a `for-in` loop instead.

## Pattern Matching

- [INST0036] **Do** use `switch` exhaustively over enums — the compiler enforces exhaustiveness, catching missing cases at compile time.
- [INST0037] **Do** use pattern matching in `if case`, `guard case`, and `for case` to destructure enum values inline — it avoids verbose switch statements for single-case checks.
- [INST0038] **Do** use `where` clauses in switch cases to add conditional logic — `case .success(let value) where value > 0:` — instead of nesting `if` inside the case body.

## Memory Management

- [INST0039] **Do** use `[weak self]` or `[unowned self]` in closures that capture `self` and may outlive the owning object — prevent retain cycles, especially in view controllers, delegates, and combine pipelines.
- [INST0040] **Do** use `[unowned self]` only when you can guarantee `self` outlives the closure — otherwise prefer `[weak self]` with `guard let self` unwrapping.
- [INST0041] **Don't** capture `self` strongly in escaping closures stored as properties — this is the most common source of memory leaks.

## Documentation

- [INST0042] **Do** write triple-slash (`///`) doc comments for all `public` and `open` symbols — include a one-line summary, parameter descriptions (`- Parameter name:`), return value (`- Returns:`), and thrown errors (`- Throws:`).
- [INST0043] **Do** use `- Note:`, `- Important:`, and `- Warning:` callouts in doc comments for non-obvious behavior.
- [INST0044] **Don't** repeat what the signature already says — focus doc comments on *why*, edge cases, preconditions, and threading expectations.

## Formatting

- [INST0045] **Do** follow the [Swift API Design Guidelines](https://www.swift.org/documentation/api-design-guidelines/) for all public API naming decisions.
