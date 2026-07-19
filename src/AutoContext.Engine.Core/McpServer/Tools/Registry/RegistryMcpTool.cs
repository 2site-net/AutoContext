namespace AutoContext.Engine.Core.McpServer.Tools.Registry;

using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;

using ModelContextProtocol.Protocol;

using JsonRpcResponse = Protocol.JsonRpc.JsonRpcResponse;

/// <summary>
/// A worker-backed MCP tool sourced from one <see cref="McpToolsRegistryEntry"/>
/// (an <c>analyze_*</c> / <c>read_*</c> tool). Every registry tool dispatches
/// uniformly through the engine's <c>McpTools.Invoke</c> handler, which spawns
/// the owning worker on demand.
/// </summary>
internal sealed class RegistryMcpTool : IMcpTool
{
    private readonly IRpcMethodHandler _handler;
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryMcpTool"/> class
    /// for one registry entry.
    /// </summary>
    /// <param name="entry">The registry entry describing the tool.</param>
    /// <param name="mcpToolsHandler">The <c>McpTools.*</c> capability handler.</param>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public RegistryMcpTool(McpToolsRegistryEntry entry, IRpcMethodHandler mcpToolsHandler)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(mcpToolsHandler);

        _handler = mcpToolsHandler;
        _name = entry.Name;
        Descriptor = new Tool
        {
            Name = entry.Name,
            Description = entry.ModelDescription,
            InputSchema = InputSchemaBuilder.Build(entry.Parameters),
        };
    }

    /// <inheritdoc />
    public Tool Descriptor { get; }

    /// <inheritdoc />
    public ValueTask<JsonRpcResponse> InvokeAsync(
        IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        var parameters = new JsonMcpToolsInvokeParams
        {
            Name = _name,
            Arguments = JsonArguments.ToElement(arguments),
        };

        return HandlerMarshaller.InvokeAsync(
            _handler,
            McpToolsMethods.Invoke,
            JsonSerializer.SerializeToElement(parameters, ProtocolJsonContext.Default.JsonMcpToolsInvokeParams),
            cancellationToken);
    }
}
