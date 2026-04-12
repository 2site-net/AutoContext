---
name: "dotnet-nuget (v1.0.0)"
description: "Use when adding, updating, removing, or reviewing NuGet package references or third-party dependencies in .NET projects."
---
# NuGet Packages & Dependencies Guidelines

- [INST0001] **Do** prefer built-in .NET libraries over third-party packages unless they add clear value (e.g., `System.Text.Json` vs `Newtonsoft.Json`).
- [INST0002] **Do** review package references in each `.csproj`/`.fsproj`.
- [INST0003] **Do** read official docs and the project's `README.md` before adding or updating a package.
- [INST0004] **Do** examine package repos for deeper insight (NuGet, GitHub, etc.).
- [INST0005] **Do** propose an official, well-maintained package only when truly needed, and explain why.
- [INST0006] **Don't** keep unused package references.
