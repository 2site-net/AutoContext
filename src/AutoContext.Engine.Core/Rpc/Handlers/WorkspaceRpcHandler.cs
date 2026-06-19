namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Workspace;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// The <c>Workspace.*</c> handler. Projects the current workspace detection
/// result (<c>Workspace.Detect</c>) and the resolved engine instance info
/// (<c>Workspace.Info</c>) from the
/// <see cref="IWorkspaceContextAccessor"/> seam. Both methods read in-memory
/// state and always succeed, so the connection keeps serving.
/// </summary>
internal sealed class WorkspaceRpcHandler : IRpcMethodHandler
{
    private readonly IWorkspaceContextAccessor _workspaceAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceRpcHandler"/>
    /// class.
    /// </summary>
    public WorkspaceRpcHandler(IWorkspaceContextAccessor workspaceAccessor)
    {
        ArgumentNullException.ThrowIfNull(workspaceAccessor);

        _workspaceAccessor = workspaceAccessor;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } =
        [WorkspaceMethods.Detect, WorkspaceMethods.Info];

    /// <inheritdoc />
    public ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        RpcHandlerResult result = request.Method switch
        {
            WorkspaceMethods.Info => HandleWorkspaceInfo(),
            _ => HandleWorkspaceDetect(),
        };

        return ValueTask.FromResult(result);
    }

    private UnaryHandlerResult HandleWorkspaceDetect()
    {
        var result = _workspaceAccessor.Current.ToWireFormat();
        return RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonWorkspaceDetectResult);
    }

    private UnaryHandlerResult HandleWorkspaceInfo()
    {
        var result = new JsonWorkspaceInfoResult
        {
            EngineVersion = EngineVersion.Value,
            IdleTimeout = _workspaceAccessor.EngineInfo.IdleTimeout,
            InstanceId = _workspaceAccessor.EngineInfo.InstanceId,
            InstanceLabel = _workspaceAccessor.EngineInfo.InstanceLabel,
            Revision = _workspaceAccessor.Revision,
        };
        return RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonWorkspaceInfoResult);
    }
}
