namespace AutoContext.Engine.Core.Tests.Support.McpServer.Tools;

using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.McpServer.Tools;

using ModelContextProtocol.Protocol;

using JsonRpcResponse = Protocol.JsonRpc.JsonRpcResponse;

/// <summary>
/// In-memory <see cref="IMcpTool"/> test double that records its invocation
/// and returns a caller-supplied <see cref="JsonRpcResponse"/>, letting the
/// adapter's routing be tested without real capability handlers.
/// </summary>
internal sealed class FakeMcpTool : IMcpTool
{
    public FakeMcpTool(string name)
    {
        Descriptor = new Tool
        {
            Name = name,
            Description = name,
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
        };
    }

    public Tool Descriptor { get; }

    public int InvokeCallCount { get; private set; }

    public IDictionary<string, JsonElement>? LastArguments { get; private set; }

    public JsonRpcResponse Response { get; set; } = new()
    {
        Result = JsonSerializer.SerializeToElement(new { ok = true }),
    };

    public ValueTask<JsonRpcResponse> InvokeAsync(
        IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        InvokeCallCount++;
        LastArguments = arguments;

        return ValueTask.FromResult(Response);
    }
}
