namespace AutoContext.Engine.Protocol.Messages.Discovery;

using System.Text.Json.Serialization;

/// <summary>
/// Response for <see cref="DiscoveryMethods.RouteForTool"/>: the
/// instructions file names whose workspace-context activation flags
/// intersect the tool's, in corpus document order, with disabled files
/// excluded.
/// </summary>
public sealed record JsonDiscoveryRouteForToolResult
{
    /// <summary>
    /// The domain-relevant instructions file names (each including the
    /// <c>.instructions.md</c> extension).
    /// </summary>
    [JsonPropertyName("instructions")]
    public IReadOnlyList<string> Instructions { get; init; } = [];
}
