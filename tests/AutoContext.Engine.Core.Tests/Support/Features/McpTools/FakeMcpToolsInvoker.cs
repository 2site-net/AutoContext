namespace AutoContext.Engine.Core.Tests.Support.Features.McpTools;

using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Protocol.Messages.McpTools;

internal sealed class FakeMcpToolsInvoker : IMcpToolsInvoker
{
    public int InvokeCallCount { get; private set; }

    public JsonElement LastArguments { get; private set; }

    public McpToolsRegistryEntry? LastTool { get; private set; }

    public JsonMcpToolsInvokeResult Result { get; set; } =
        new JsonMcpToolsInvokeOkResult { Name = "tool", Content = [] };

    public Exception? ThrowOnInvoke { get; set; }

    public Task<JsonMcpToolsInvokeResult> InvokeAsync(
        McpToolsRegistryEntry tool,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        cancellationToken.ThrowIfCancellationRequested();

        InvokeCallCount++;
        LastTool = tool;
        LastArguments = arguments.Clone();

        if (ThrowOnInvoke is { } ex)
        {
            throw ex;
        }

        return Task.FromResult(Result);
    }
}
