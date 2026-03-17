---
description: "Use when creating or editing Blazor Razor components, render modes, cascading parameters, EventCallback, Virtualize, or JS interop."
applyTo: "**/*.razor,**/*.razor.cs"
---
# Blazor Guidelines

* **Do** keep UI logic inside Razor components; move data access, business rules, and other cross-cutting concerns to injectable services.
* **Do** bind data with `[Parameter]`; reserve `[CascadingParameter]` / `<CascadingValue>` for truly global context (auth, theme, culture). Use `IsFixed="true"` when the value never changes.
* **Do** notify parents with `EventCallback<T>`; it supports `async/await` and marshals to the renderer thread.
* **Do** provide a stable identity with `@key` when rendering items whose order can change.
* **Do** override `ShouldRender` or rely on immutable parameters to skip unnecessary rerenders; throttle `StateHasChanged()` calls triggered by timers or streams.
* **Do** virtualise large lists with `<Virtualize ItemsProvider=...>` or a paging grid.
* **Do** register services with the correct lifetime: `Scoped` for per-user/session data, `Singleton` for stateless utilities. Inject them with `[Inject]`/`@inject`.
* **Do** dispose `IDisposable` / `IAsyncDisposable` resources in `Dispose` / `DisposeAsync` and release JS object references.
* **Do** keep `OnInitialized{Async}` light; for heavy work defer to `OnAfterRenderAsync` (first-render) or background services, especially during prerender.
* **Do** choose render modes deliberately: `Static` for SEO and minimal payload, `Streaming` for progressive hydration, or an interactive mode when client interactivity is required.
* **Do** use JS interop for DOM-intensive operations (canvas, animations, complex drag-and-drop) and simple client-side interactions that don't require server round-trips (e.g., toggling a dialog).
* **Do** extract child components when a component handles multiple distinct responsibilities — each component should own a single concern.
* **Do** scope `<ErrorBoundary>` narrowly around failure-prone subtrees rather than wrapping entire pages; call `Recover()` in `OnParametersSet` so navigation clears the error state, and use `<ErrorContent>` for a custom fallback.
* **Do** catch `JSDisconnectedException` in `DisposeAsync` when releasing JS interop references on Blazor Server--the circuit may already be disconnected, so log and suppress.
* **Do** create a `CancellationTokenSource`, pass its token to long-running async calls, and cancel it in `Dispose`/`DisposeAsync` to stop orphaned work when the component is removed.
* **Do** apply `[StreamRendering]` to routable components that load async data so placeholder content renders progressively instead of showing a full loading page.
* **Do** use `[PersistentState]` (or `PersistComponentState` on older TFMs) to preserve component state across prerender-to-interactive transitions, avoiding redundant data fetches.
* **Do** use either `Model=` or `EditContext=` on `<EditForm>`--never both. Use `Model=` for simple cases; use `EditContext=` only when you need manual control (dynamic validation, form reset).
* **Do** prefer `OnValidSubmit`/`OnInvalidSubmit` over `OnSubmit`--the split callbacks handle validation automatically. Use `OnSubmit` only when custom validation logic is required.
* **Do** decorate form-bound parameters with `[SupplyParameterFromForm]` in components rendered with static SSR or enhanced form handling--without it the model won't bind from the POST body.
* **Don't** implement business logic in JavaScript — keep it in C# and use JS only for DOM manipulation and client-side UI behavior.
* **Don't** mutate `[Parameter]` values or cascaded context after first render--treat them as read-only inputs.
* **Don't** cascade large mutable objects or frequently changing data; publish events or use DI services instead.
* **Don't** call `StateHasChanged()` in a tight loop or inside `OnAfterRender` without a guard--it can cause infinite renders.
* **Don't** block the renderer thread with heavy CPU work or synchronous I/O--offload with `Task.Run` or a background service.
* **Don't** trigger JS interop in constructors or before the DOM exists; call it from `OnAfterRenderAsync`.
* **Don't** store per-user state in singleton services on Blazor Server--circuits share the same instance.
* **Don't** add custom get/set logic to `[Parameter]` or `[CascadingParameter]` properties--declare them as plain auto-properties (`{ get; set; }`) because the framework overwrites them on every render.