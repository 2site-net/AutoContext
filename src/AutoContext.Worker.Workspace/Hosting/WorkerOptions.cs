namespace AutoContext.Worker.Workspace.Hosting;

/// <summary>
/// Strongly-typed options bound from configuration (command-line args
/// <c>--pipe</c> and <c>--workspace-root</c>).
/// </summary>
internal sealed class WorkerOptions
{
    /// <summary>
    /// Named pipe the worker listens on for per-task requests.
    /// </summary>
    public string Pipe { get; init; } = string.Empty;

    /// <summary>
    /// Absolute path to the workspace root (used by
    /// <c>get_autocontext_config_file</c> to locate <c>.autocontext.json</c>).
    /// </summary>
    public string WorkspaceRoot { get; init; } = string.Empty;
}
