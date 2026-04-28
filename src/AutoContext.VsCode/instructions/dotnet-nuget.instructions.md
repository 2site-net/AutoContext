---
name: "dotnet-nuget (v1.0.0)"
description: "Apply when adding, updating, removing, or reviewing NuGet package references in .NET projects."
---

# NuGet Packages & Dependencies Instructions

## MCP Tool Validation

After editing any `.csproj` (adding, removing, or updating
`PackageReference` entries), call the `analyze_nuget_references`
MCP tool with the full `.csproj` XML as `content`. Treat any reported
violation as blocking — fix it before reporting the work as done.

## Rules

- [INST0001] **Do** prefer built-in .NET libraries over third-party packages unless they add clear value (e.g., `System.Text.Json` vs `Newtonsoft.Json`).
- [INST0002] **Do** review package references in each `.csproj`/`.fsproj`.
- [INST0003] **Do** read official docs and the project's `README.md` before adding or updating a package.
- [INST0004] **Do** examine package repos for deeper insight (NuGet, GitHub, etc.).
- [INST0005] **Do** propose an official, well-maintained package only when truly needed, and explain why.
- [INST0006] **Don't** keep unused package references.
