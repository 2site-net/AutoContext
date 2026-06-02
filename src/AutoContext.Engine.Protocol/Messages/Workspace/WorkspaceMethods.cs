namespace AutoContext.Engine.Protocol.Messages.Workspace;

/// <summary>
/// JSON-RPC method-name constants for the <c>Workspace.*</c> family —
/// the engine's read-only view of the workspace it is pinned to.
/// Kept in the protocol assembly so handlers and transports share
/// one spelling of each dotted method name per
/// <c>design § RPC surface</c>, and grouped alongside the workspace
/// DTOs (<see cref="JsonWorkspaceDetectResult"/>,
/// <see cref="JsonWorkspaceInfoResult"/>) they pair with.
/// </summary>
public static class WorkspaceMethods
{
    /// <summary>
    /// Detects the workspace's technology shape. Runs against the
    /// engine's own <c>--workspace</c> path (not an arbitrary path);
    /// takes no params and returns a
    /// <see cref="JsonWorkspaceDetectResult"/> — the full flag set
    /// plus the derived extension index. Stateless, idempotent read.
    /// </summary>
    public const string Detect = "Workspace.Detect";

    /// <summary>
    /// Reads engine-process metadata for the pinned workspace. Takes
    /// no params and returns a <see cref="JsonWorkspaceInfoResult"/> —
    /// engine version, the <c>(instanceId, revision)</c> pair,
    /// instance label, and idle-timeout state. Distinct from
    /// <see cref="Detect"/>, which describes the workspace contents
    /// rather than the engine serving it.
    /// </summary>
    public const string Info = "Workspace.Info";
}
