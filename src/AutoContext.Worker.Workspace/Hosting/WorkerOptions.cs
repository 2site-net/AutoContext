namespace AutoContext.Worker.Workspace.Hosting;

/// <summary>
/// Strongly-typed options bound from configuration (command-line argument
/// <c>--workspace-root</c>) for workspace-specific tasks.
/// </summary>
/// <remarks>
/// The shared host options (<c>--pipe</c>, ready marker) live on
/// <see cref="Framework.Workers.WorkerHostOptions"/>.
/// </remarks>
internal sealed class WorkerOptions
{
    /// <summary>
    /// Absolute path to the workspace root (used by
    /// <c>get_autocontext_config_file</c> to locate <c>.autocontext.json</c>).
    /// </summary>
    public string WorkspaceRoot { get; init; } = string.Empty;
}
