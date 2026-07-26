namespace AutoContext.Client.Core.Engine.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Discovery;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's <c>Discovery.*</c> RPC family over a
/// live <see cref="EngineConnection"/>. Routes a raw prompt to the
/// strongly-relevant tools and instructions files, and maps a tool
/// identity to the instructions files whose domain it shares — both
/// filtered by the engine's current disabled state.
/// </summary>
public sealed class DiscoveryRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="DiscoveryRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public DiscoveryRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Scans <paramref name="prompt"/> for category words and file
    /// extensions and returns the matched categories and extensions
    /// together with the strongly-relevant MCP tools and instructions
    /// files.
    /// </summary>
    /// <param name="prompt">The user prompt to route. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonDiscoveryRouteForPromptResult> RouteForPromptAsync(
        string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonDiscoveryRouteForPromptParams { Prompt = prompt },
            ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptParams);

        return _connection.InvokeAsync(
            DiscoveryMethods.RouteForPrompt,
            parameters,
            ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptResult,
            cancellationToken);
    }

    /// <summary>
    /// Maps the MCP tool named <paramref name="name"/> to the
    /// instructions files whose workspace-context activation flags it
    /// shares.
    /// </summary>
    /// <param name="name">MCP tool name. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonDiscoveryRouteForToolResult> RouteForToolAsync(
        string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonDiscoveryRouteForToolParams { Name = name },
            ProtocolJsonContext.Default.JsonDiscoveryRouteForToolParams);

        return _connection.InvokeAsync(
            DiscoveryMethods.RouteForTool,
            parameters,
            ProtocolJsonContext.Default.JsonDiscoveryRouteForToolResult,
            cancellationToken);
    }
}
