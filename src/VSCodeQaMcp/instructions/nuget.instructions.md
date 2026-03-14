---
description: "Use when adding, updating, removing, or reviewing NuGet package references or third-party dependencies in C# projects."
---
# NuGet Packages & Dependencies Guidelines

- **Do** prefer built-in .NET libraries over third-party packages unless they add clear value (e.g., `System.Text.Json` vs `Newtonsoft.Json`).
- **Do** review package references in each `.csproj`.
- **Do** read official docs and the project's `README.md` before adding or updating a package.
- **Do** examine package repos for deeper insight (NuGet, GitHub, etc.).
- **Do** propose an official, well-maintained package only when truly needed, and explain why.
- **Don't** keep unused package references.
