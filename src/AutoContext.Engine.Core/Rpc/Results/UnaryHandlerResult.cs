namespace AutoContext.Engine.Core.Rpc.Results;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Result that writes exactly one <see cref="JsonRpcResponse"/>
/// frame on the wire. The canonical JSON-RPC 2.0 request/response
/// shape — every request/response handler returns this.
/// </summary>
/// <param name="Response">The JSON-RPC 2.0 response frame to
/// write. The handler is responsible for setting either
/// <c>Result</c> or <c>Error</c> (never both) and for echoing the
/// request id — when the handler leaves <c>Id</c> at
/// <see cref="JsonValueKind.Undefined"/> the processor normalises
/// it from the original request id.</param>
/// <param name="Continuation">See <see cref="RpcHandlerResult.Continuation"/>.</param>
/// <param name="PostFlush">See <see cref="RpcHandlerResult.PostFlush"/>.</param>
internal sealed record UnaryHandlerResult(
    JsonRpcResponse Response,
    Continuation Continuation = Continuation.Continue,
    Func<Task>? PostFlush = null)
    : RpcHandlerResult(Continuation, PostFlush);
