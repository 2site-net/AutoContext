namespace AutoContext.Engine.Protocol.Messages.Discovery;

using System.Text.Json.Serialization;

/// <summary>
/// Request params for <see cref="DiscoveryMethods.RouteForTool"/>: the
/// MCP tool name whose domain-relevant instructions files the caller
/// wants. An unknown or flagless tool yields an empty route.
/// </summary>
public sealed record JsonDiscoveryRouteForToolParams
{
    /// <summary>The MCP tool name to route.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
