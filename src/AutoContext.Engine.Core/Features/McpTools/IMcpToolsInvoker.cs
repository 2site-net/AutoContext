namespace AutoContext.Engine.Core.Features.McpTools;

using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Protocol.Messages.McpTools;

/// <summary>
/// Dispatch seam for a single MCP tool invocation. Implementations ensure
/// the target worker is ready, send the tool call over the worker pipe, and
/// map the worker response into the wire-level invoke envelope.
/// </summary>
internal interface IMcpToolsInvoker
{
    /// <summary>
    /// Invokes <paramref name="tool"/> with <paramref name="arguments"/>.
    /// </summary>
    /// <param name="tool">The catalog entry to dispatch.</param>
    /// <param name="arguments">Validated tool arguments object.</param>
    /// <param name="cancellationToken">Caller cancellation token.</param>
    /// <returns>The invoke envelope arm produced by the dispatch path.</returns>
    Task<JsonMcpToolsInvokeResult> InvokeAsync(
        McpToolsRegistryEntry tool,
        JsonElement arguments,
        CancellationToken cancellationToken);
}
