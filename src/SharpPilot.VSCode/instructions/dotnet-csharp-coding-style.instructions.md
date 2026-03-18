---
description: "C# code style rules: member ordering, formatting, language features, nullability, and documentation conventions."
applyTo: "**/*.cs"
---
# Coding Style

- **Do** use current C# features when they enhance clarity (e.g., file‑scoped namespaces, raw string literals, collection expressions, pattern matching, `record`, `required`, `init`).
- **Do** order members by kind (constants, static fields, fields, constructors, delegates, events, enums, properties, indexers, methods, operators, nested types), then by access level (public → private), then static before instance, then alphabetically.
- **Do** group related statements into logical paragraphs separated by blank lines.
- **Do** insert a blank line before control flow statements (`if`, `for`, `foreach`, `while`, `do`, `switch`, `try`, `using`, `lock`).
- **Do** insert a blank line between variable declarations and their usage.
- **Do** place expression-body arrows (`=>`) on the line below the method signature, not at the end of it.
- **Do** always use curly braces for control flow statements, even for single-line bodies — except single-line guard clauses (early `return`/`throw`).
- **Do** name private instance fields with a leading underscore and camelCase (e.g., `_workerQueue`); name private static fields with `s_` prefix (e.g., `s_instance`) — this is the .NET Runtime team convention.
- **Do** use language keywords for built-in types instead of BCL type names — `string` not `String`, `int` not `Int32`, `bool` not `Boolean`.
- **Do** use PascalCase for all constants, both fields and local constants.
- **Do** use `var` when the type is apparent from the right-hand side (`var users = new List<User>()`); use an explicit type when it isn't — prefer clarity over brevity.
- **Do** use `nameof(x)` instead of the string literal `"x"` when referring to a symbol — survives renames and refactors.
- **Do** use pattern-based null checks — `if (x is null)` and `if (x is not null)` — instead of `== null` / `!= null`; pattern syntax is not affected by overloaded equality operators.
- **Do** use the braceless `using` declaration for disposables instead of a `try/finally` block whose only purpose is calling `Dispose` — `using Font font = new(...);` is cleaner and scopes disposal to the enclosing block.
- **Do** keep `#nullable enable` on for every project and treat nullable warnings as errors.
- **Do** add XML doc comments (`/// <summary>`) to public and protected types, members, and parameters — keep them brief and focused on intent, not implementation.
- **Don't** suppress nullable warnings with `!` unless you've proved safety.
- **Don't** use `#pragma warning disable` — prefer `[SuppressMessage]` attributes with a justification.
- **Don't** nest conditional expressions (`?:`, `??`) — extract to a method, local variable, or use `if`/`else` for clarity.
- **Don't** write long LINQ chains — break them into intermediate variables or extract into a method when readability suffers.
- **Don't** use `#region` – they hide code structure and make it harder to navigate.
- **Don't** use decorative section-header comments (e.g., `// ── Lifecycle ──────`) — organize code through consistent member ordering instead.
