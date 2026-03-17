---
description: "Use when creating or editing Razor component files (.razor), organizing directives, structuring code-behind files, or working with Razor syntax and markup."
applyTo: "**/*.razor,**/*.razor.cs"
---
# Razor Guidelines

* **Do** name component files in **PascalCase** matching the component class (e.g., `ProductDetail.razor`); keep one component per `.razor` file.
* **Do** separate components into dedicated files — `.razor` for markup, `.razor.cs` for code-behind, `.razor.css` for scoped styles — avoid `@code` blocks in `.razor` files.
* **Do** order directives at the top of `.razor` files with no blank lines between them, followed by one blank line before markup:
  1. `@page`
  2. `@rendermode`
  3. `@using` — grouped: `System` → `Microsoft` → third-party → app namespaces (alphabetical within each group)
  4. Other directives alphabetically (`@attribute`, `@implements`, `@inject`, `@layout`, `@typeparam`)
* **Do** use `_Imports.razor` for `@using` directives shared across a folder and its subfolders — avoid repeating the same `@using` in every component.
* **Do** use kebab-case route templates matching the component name (e.g., `ProductDetail` → `@page "/product-detail"`).
* **Do** mark required component parameters with `[EditorRequired]` to surface warnings at design and build time.
* **Do** prefer `@bind` with `@bind:after` over manual event wiring for two-way binding that needs post-bind async work.
* **Do** prefer `@bind:get`/`@bind:set` over legacy `value`/`@onchange` pairs for two-way binding — the bind syntax prevents value-synchronization bugs and makes intent explicit.
* **Do** add `@formname` to plain HTML `<form>` elements when using enhanced form handling or static SSR so the framework can route form posts to the correct handler.
* **Don't** use `builder.AddMarkupContent` with user-supplied strings — use `builder.AddContent` (text-safe) to prevent XSS.
* **Don't** nest Razor control-flow blocks (`@if`, `@foreach`) more than two levels deep — extract a child component or `RenderFragment` instead.
* **Don't** write user-supplied data to the DOM via `innerHTML` or raw markup injection — Razor's default text rendering is XSS-safe; bypass it only with trusted content.
