namespace AutoContext.Engine.Core.Tests.Support.Rpc;

using System.Text.Json;

using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// In-memory <see cref="IRpcMethodHandler"/> test double that records the
/// last request it received and returns a caller-supplied
/// <see cref="JsonRpcResponse"/> as a <see cref="UnaryHandlerResult"/>.
/// Lets the <c>McpSdkAdapter</c> marshalling be tested without constructing
/// the real capability handlers or their dependency graphs.
/// </summary>
internal sealed class FakeRpcMethodHandler : IRpcMethodHandler
{
    public JsonRpcRequest? LastRequest { get; private set; }

    public int InvokeCallCount { get; private set; }

    public JsonRpcResponse Response { get; set; } = new()
    {
        Result = JsonSerializer.SerializeToElement(new { ok = true }),
    };

    public IReadOnlyCollection<string> Methods { get; } = [];

    public ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        LastRequest = request;
        InvokeCallCount++;

        return ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(Response));
    }
}
