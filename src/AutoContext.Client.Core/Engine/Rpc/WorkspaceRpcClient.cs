namespace AutoContext.Client.Core.Engine.Rpc;

using AutoContext.Engine.Protocol.Messages.Workspace;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's <c>Workspace.*</c> RPC family over a
/// live <see cref="EngineConnection"/>. Reads the engine's detected
/// technology shape for the pinned workspace and the engine-process
/// metadata serving it; both are parameter-less, idempotent reads.
/// </summary>
public sealed class WorkspaceRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="WorkspaceRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public WorkspaceRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Detects the workspace's technology shape — the full flag set
    /// plus the derived extension index the engine holds for its
    /// <c>--workspace</c> path.
    /// </summary>
    public Task<JsonWorkspaceDetectResult> DetectAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            WorkspaceMethods.Detect,
            parameters: null,
            ProtocolJsonContext.Default.JsonWorkspaceDetectResult,
            cancellationToken);

    /// <summary>
    /// Reads engine-process metadata for the pinned workspace — engine
    /// version, the <c>(instanceId, revision)</c> pair, instance label,
    /// and idle-timeout state.
    /// </summary>
    public Task<JsonWorkspaceInfoResult> InfoAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            WorkspaceMethods.Info,
            parameters: null,
            ProtocolJsonContext.Default.JsonWorkspaceInfoResult,
            cancellationToken);
}
