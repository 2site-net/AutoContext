namespace AutoContext.Engine.Core.McpServer.Tools;

using System.Text.Json;

using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Marshals an MCP <c>tools/call</c> into the engine capability handler that
/// owns the method, returning the handler's <see cref="JsonRpcResponse"/>.
/// Both the stdio and the daemon pipe transports drive the same
/// <see cref="IRpcMethodHandler"/> instances, so a tool served over stdio
/// answers byte-identically to its pipe RPC.
/// </summary>
internal static class HandlerMarshaller
{
    /// <summary>
    /// Invokes <paramref name="handler"/>'s <paramref name="method"/> with
    /// the supplied serialized <paramref name="parameters"/> and returns the
    /// unary response frame.
    /// </summary>
    public static async ValueTask<JsonRpcResponse> InvokeAsync(
        IRpcMethodHandler handler,
        string method,
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var request = new JsonRpcRequest { Method = method, Params = parameters };
        var result = await handler.InvokeAsync(request, cancellationToken).ConfigureAwait(false);

        return ((UnaryHandlerResult)result).Response;
    }
}
