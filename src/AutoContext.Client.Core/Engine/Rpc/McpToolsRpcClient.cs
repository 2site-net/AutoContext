namespace AutoContext.Client.Core.Engine.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's <c>McpTools.*</c> RPC family over a
/// live <see cref="EngineConnection"/>. Lists the engine's MCP tool
/// catalog and invokes a tool by name — the pipe-side counterpart of
/// MCP's <c>tools/call</c>. <see cref="InvokeAsync"/> returns the
/// discriminated <see cref="JsonMcpToolsInvokeResult"/> so callers see
/// the engine's <c>ok</c> / <c>tool-error</c> / <c>schema-error</c> /
/// <c>disabled</c> / <c>not-found</c> outcome without a nullable.
/// </summary>
public sealed class McpToolsRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="McpToolsRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public McpToolsRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Invokes the MCP tool named <paramref name="name"/> with the
    /// opaque <paramref name="arguments"/> object, returning the
    /// discriminated invocation result.
    /// </summary>
    /// <param name="name">MCP tool name. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="arguments">Tool arguments passed through verbatim,
    /// or <see langword="null"/> for a tool that takes none.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonMcpToolsInvokeResult> InvokeAsync(
        string name, JsonElement? arguments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonMcpToolsInvokeParams { Name = name, Arguments = arguments },
            ProtocolJsonContext.Default.JsonMcpToolsInvokeParams);

        return _connection.InvokeAsync(
            McpToolsMethods.Invoke,
            parameters,
            ProtocolJsonContext.Default.JsonMcpToolsInvokeResult,
            cancellationToken);
    }

    /// <summary>
    /// Lists the engine's MCP tool catalog — one identity row per tool,
    /// carrying the engine-resolved per-tool disabled state.
    /// </summary>
    public Task<JsonMcpToolsListResult> ListAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            McpToolsMethods.List,
            parameters: null,
            ProtocolJsonContext.Default.JsonMcpToolsListResult,
            cancellationToken);
}
