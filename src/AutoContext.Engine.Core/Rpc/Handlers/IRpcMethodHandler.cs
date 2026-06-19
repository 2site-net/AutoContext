namespace AutoContext.Engine.Core.Rpc.Handlers;

using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Handles a cohesive group of JSON-RPC methods on an RPC connection.
/// <see cref="Policies.DispatchPolicy"/> composes the registered handlers
/// into a method-keyed router and delegates each request to the handler
/// that declares the method in <see cref="Methods"/>.
/// </summary>
internal interface IRpcMethodHandler
{
    /// <summary>
    /// Gets the JSON-RPC method names this handler serves. Each name must
    /// be unique across all registered handlers.
    /// </summary>
    IReadOnlyCollection<string> Methods { get; }

    /// <summary>
    /// Handles a request for one of the declared <see cref="Methods"/>.
    /// </summary>
    /// <param name="request">The JSON-RPC request to handle.</param>
    /// <param name="cancellationToken">Signals connection teardown.</param>
    ValueTask<RpcHandlerResult> InvokeAsync(JsonRpcRequest request, CancellationToken cancellationToken);
}
