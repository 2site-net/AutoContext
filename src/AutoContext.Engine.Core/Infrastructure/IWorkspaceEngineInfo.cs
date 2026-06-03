namespace AutoContext.Engine.Core.Infrastructure;

/// <summary>
/// Read-only view over the engine-instance metadata the workspace
/// subsystem cares about — the workspace folder being scanned plus the
/// per-spawn identity, label, and idle window surfaced to readers via
/// <c>Workspace.Info</c>. Implemented by <see cref="EngineOptions"/> so
/// the <see cref="Workspace.Context.WorkspaceContextDetector"/> and the
/// <see cref="Workspace.Context.IWorkspaceContextAccessor"/> seam depend
/// on this narrow contract rather than on the full composition surface.
/// </summary>
internal interface IWorkspaceEngineInfo
{
    /// <summary>
    /// The configured idle-shutdown window for this engine instance.
    /// </summary>
    TimeSpan IdleTimeout { get; }

    /// <summary>
    /// Per-spawn UUID this engine instance is bound to.
    /// </summary>
    Guid InstanceId { get; }

    /// <summary>
    /// Optional human-readable label attached by the launcher.
    /// </summary>
    string InstanceLabel { get; }

    /// <summary>
    /// Absolute path of the workspace folder this engine instance scans.
    /// </summary>
    string WorkspacePath { get; }
}
