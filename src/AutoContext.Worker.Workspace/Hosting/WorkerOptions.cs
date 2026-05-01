namespace AutoContext.Worker.Workspace.Hosting;

/// <summary>
/// Strongly-typed options bound from configuration (command-line argument
/// <c>--workspace-root</c>) for workspace-specific tasks.
/// </summary>
/// <remarks>
/// The shared host options (<c>--pipe</c>, ready marker) live on
/// <see cref="Framework.Workers.WorkerHostOptions"/>. Currently no task
/// in this worker reads <see cref="WorkspaceRoot"/>; the property and
/// the <c>--workspace-root</c> switch are accepted for backward
/// compatibility with the extension and reserved for future
/// workspace-scoped tasks.
/// </remarks>
internal sealed class WorkerOptions
{
    /// <summary>
    /// Absolute path to the workspace root supplied via
    /// <c>--workspace-root</c>. Reserved for future workspace-scoped
    /// tasks; currently unused.
    /// </summary>
    public string WorkspaceRoot { get; init; } = string.Empty;
}
