---
description: "PHP coding style instructions: naming conventions, type safety, error handling, OOP, functions, security, database access, and documentation."
applyTo: "**/*.php"
version: "1.0.0"
---
# PHP Coding Style

## Naming

- [INST0001] **Do** use `PascalCase` for class, interface, trait, and enum names; `camelCase` for methods and properties; `snake_case` for functions in non-OOP code; `SCREAMING_SNAKE_CASE` for constants.
- [INST0002] **Do** suffix interfaces with `Interface` or prefix with a role noun (`Renderable`, `Cacheable`) — be consistent within the project.
- [INST0003] **Don't** use Hungarian notation or type prefixes — `$user` not `$objUser`, `$count` not `$intCount`.

## Type Safety

- [INST0004] **Do** declare `strict_types=1` at the top of every file — `declare(strict_types=1);` prevents implicit type coercion and catches type bugs at call time.
- [INST0005] **Do** type-hint all function/method parameters, return types, and class properties — use union types (`string|int`), intersection types (`Countable&Iterator`), and `?Type` for nullable where appropriate.
- [INST0006] **Do** use enums (`enum Status: string { ... }`) instead of class constants for fixed sets of related values — backed enums provide type safety and prevent invalid values.
- [INST0007] **Do** use `readonly` properties and `readonly` classes for value objects and DTOs — it guarantees immutability after construction.
- [INST0008] **Don't** rely on loose comparisons (`==`, `!=`) — use strict comparisons (`===`, `!==`) to avoid PHP's type-juggling surprises.

## Error Handling

- [INST0009] **Do** throw specific exception types that extend a project or library base exception — callers should be able to catch by domain (`OrderException`) not just `\Exception`.
- [INST0010] **Do** use `try`/`catch` at service boundaries and let exceptions propagate through business logic — avoid catching exceptions only to re-throw them without added context.
- [INST0011] **Do** chain exceptions with `throw new DomainException('context', 0, $previous)` to preserve the original cause.
- [INST0012] **Don't** use `@` error suppression — it hides real problems and makes debugging nearly impossible.
- [INST0013] **Don't** catch `\Throwable` or `\Exception` broadly without logging or re-throwing — silently swallowed errors corrupt application state.

## OOP & Design

- [INST0014] **Do** prefer composition over inheritance — inject collaborators through the constructor rather than extending a base class for reuse.
- [INST0015] **Do** program to interfaces — type-hint parameters and return types against interfaces, not concrete classes, to enable substitution and testing.
- [INST0016] **Do** use constructor promotion (`public function __construct(private readonly string $name)`) to reduce boilerplate on PHP 8.0+.
- [INST0017] **Do** keep classes focused on a single responsibility — if a class name contains "And" or "Manager", it likely does too much.
- [INST0018] **Do** mark classes `final` by default — open them for extension only when a clear extension point is designed and documented.
- [INST0019] **Don't** use magic methods (`__get`, `__set`, `__call`) for public API — they defeat static analysis, auto-completion, and refactoring tools.

## Functions & Methods

- [INST0020] **Do** use named arguments for functions with boolean flags or many optional parameters — `array_filter($items, callback: $fn, mode: ARRAY_FILTER_USE_KEY)` is clearer than positional arguments.
- [INST0021] **Do** use `match` expressions instead of `switch` when mapping a value to a result — `match` is strict, returns a value, and has no fall-through.
- [INST0022] **Do** return early to avoid deep nesting — guard clauses at the top of a method improve readability.
- [INST0023] **Don't** pass more than three or four positional parameters — group related parameters into a value object or DTO.

## Arrays & Collections

- [INST0024] **Do** use short array syntax (`[]`) — never `array()`.
- [INST0025] **Do** use array functions (`array_map`, `array_filter`, `array_reduce`) or `foreach` for collection operations — choose whichever reads more clearly for the task.
- [INST0026] **Do** use the spread operator (`...$args`) for variadic arguments and array unpacking instead of `call_user_func_array` or manual indexing.
- [INST0027] **Don't** use arrays as ad-hoc data structures for typed data — create a class or a named tuple-like value object so the shape is explicit and type-checked.

## Security

- [INST0028] **Do** use parameterised queries (PDO prepared statements or an ORM's query builder) for all database access — never interpolate user input into SQL strings.
- [INST0029] **Do** validate and sanitise all external input at system boundaries — use filter functions (`filter_var`, `filter_input`) or a validation library.
- [INST0030] **Do** escape output for its context — `htmlspecialchars($value, ENT_QUOTES, 'UTF-8')` for HTML, `json_encode` for JSON, parameterised queries for SQL.
- [INST0031] **Don't** use `eval`, `preg_replace` with the `e` modifier, or `extract` — they enable code injection and make static analysis impossible.
- [INST0032] **Don't** store secrets in source code or version control — use environment variables or a secrets manager.

## Autoloading & Namespaces

- [INST0033] **Do** follow PSR-4 autoloading — one class per file, the namespace maps to the directory structure, and the file name matches the class name.
- [INST0034] **Do** use Composer for dependency management and autoloading — never manually `require` or `include` class files except for bootstrap or configuration.
- [INST0035] **Don't** define functions or execute side-effects in files that declare classes — keep declaration files and script files separate.

## Documentation

- [INST0036] **Do** write PHPDoc blocks (`/** ... */`) for all public classes, methods, and functions — include `@param`, `@return`, `@throws`, and a one-sentence summary.
- [INST0037] **Do** omit `@param` and `@return` tags when the native type-hint already conveys the full type — PHPDoc should add information (descriptions, generics, template types), not duplicate the signature.
- [INST0038] **Don't** restate what the code already says — focus doc comments on *why* a decision was made, invariants, and non-obvious behaviour.
