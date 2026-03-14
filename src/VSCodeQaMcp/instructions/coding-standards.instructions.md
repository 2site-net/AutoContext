---
description: "Use when writing C# code: naming conventions, nullability, C# language features, file organization, XML documentation, and API design."
applyTo: "**/*.cs"
---
# Coding Standards

- **Do** follow .NET design guidelines and common C# conventions unless noted below.
- **Do** use current .NET features when they deliver measurable performance gains.
- **Do** use current C# features when they enhance clarity (e.g., file‑scoped namespaces, raw string literals, collection expressions, pattern matching, `record`, `required`, `init`).
- **Do** validate inputs at system boundaries.
- **Do** use `ArgumentException.ThrowIf*`, `ArgumentNullException.ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIf*` and similar static throw helpers for precondition checks.
- **Do** give descriptive names that convey intent — prefer `retriesRemaining` over `n` and `GetUserById` over `GetUser`.
- **Do** prefix interfaces with `I` (e.g., `IMyType`).
- **Do** suffix extension classes with `Extensions` (e.g., `MyTypeExtensions`).
- **Do** suffix async methods with `Async` (e.g., `GetDataAsync()`).
- **Do** keep a single type per file and name the file after that type (e.g., `User.cs` for `User` class).
- **Do** place each type in a folder that reflects its role and namespace, naming the folder after the namespace.
- **Do** keep `#nullable enable` on for every project and treat nullable warnings as errors.
- **Do** comment the non-obvious "why" — algorithms, design trade-offs, and anything a future reader might find surprising — not the "what".
- **Do** add XML doc comments (`/// <summary>`) to public and protected types, members, and parameters — keep them brief and focused on intent, not implementation.
- **Don't** suppress nullable warnings with `!` unless you've proved safety.
- **Don't** use `#pragma warning disable` — prefer `[SuppressMessage]` attributes with a justification.
- **Don't** expose internal implementation details in public APIs; use interfaces or abstractions instead.
- **Don't** nest conditional expressions (`?:`, `??`) — extract to a method, local variable, or use `if`/`else` for clarity.
- **Don't** write long LINQ chains — break them into intermediate variables or extract into a method when readability suffers.
