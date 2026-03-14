---
description: "C# code style rules: member ordering, blank lines, expression bodies, curly braces, and formatting conventions."
applyTo: "**/*.cs"
---
# Code Style

- **Do** order members by kind (constants, static fields, fields, constructors, delegates, events, enums, properties, indexers, methods, operators, nested types), then by access level (public → private), then static before instance, then alphabetically.
- **Do** group related statements into logical paragraphs separated by blank lines.
- **Do** insert a blank line before control flow statements (`if`, `for`, `foreach`, `while`, `do`, `switch`, `try`, `using`, `lock`).
- **Do** insert a blank line between variable declarations and their usage.
- **Do** place expression-body arrows (`=>`) on the line below the method signature, not at the end of it.
- **Do** always use curly braces for control flow statements, even for single-line bodies — except single-line guard clauses (early `return`/`throw`).
- **Don't** use `#region` – they hide code structure and make it harder to navigate.
- **Don't** use decorative section-header comments (e.g., `// ── Lifecycle ──────`) — organize code through consistent member ordering instead.
