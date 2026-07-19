namespace AutoContext.Engine.Core.McpServer.Tools;

using System.Collections.Generic;
using System.Text.Json;

using ModelContextProtocol.Protocol;

using JsonRpcResponse = Protocol.JsonRpc.JsonRpcResponse;

/// <summary>
/// A single MCP tool: its advertised <see cref="Descriptor"/> and how to
/// invoke itself. A leaf carries no routing — the adapter routes each
/// <c>tools/call</c> to the leaf whose <see cref="Tool.Name"/> matches, so
/// adding a tool never touches the adapter.
/// </summary>
internal interface IMcpTool
{
    /// <summary>The advertised <c>tools/list</c> contract for this tool.</summary>
    Tool Descriptor { get; }

    /// <summary>
    /// Invokes the tool with the MCP call arguments and returns the owning
    /// capability handler's response frame.
    /// </summary>
    /// <param name="arguments">The MCP call arguments.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    ValueTask<JsonRpcResponse> InvokeAsync(
        IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken);
}
