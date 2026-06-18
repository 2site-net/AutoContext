namespace AutoContext.Engine.Core.Features.McpTools;

using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Protocol.Messages.McpTools;

/// <summary>
/// No-op fallback for <see cref="IMcpToolsInvoker"/> used when the host did
/// not wire worker dispatch. Returns a deterministic <c>tool-error</c>
/// envelope so callers get a stable response shape.
/// </summary>
internal sealed class McpToolsInvokerNoop : IMcpToolsInvoker
{
    private McpToolsInvokerNoop()
    {
    }

    public static McpToolsInvokerNoop Instance { get; } = new();

    /// <inheritdoc/>
    public Task<JsonMcpToolsInvokeResult> InvokeAsync(
        McpToolsRegistryEntry tool,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        cancellationToken.ThrowIfCancellationRequested();

        var content = JsonSerializer.SerializeToElement(
            new
            {
                type = "text",
                text = "McpTools.Invoke is unavailable because worker dispatch is not configured.",
            });

        return Task.FromResult<JsonMcpToolsInvokeResult>(
            new JsonMcpToolsInvokeToolErrorResult
            {
                Name = tool.Name,
                Content = [content],
            });
    }
}
