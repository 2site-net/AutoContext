namespace AutoContext.Engine.Protocol.Messages.Discovery;

using System.Text.Json.Serialization;

/// <summary>
/// Response for <see cref="DiscoveryMethods.RouteForPrompt"/>: the
/// category words and file extensions the prompt matched, plus the
/// strongly-relevant MCP tool names and instructions file names, with the
/// current disabled state already applied to the two actionable lists.
/// </summary>
public sealed record JsonDiscoveryRouteForPromptResult
{
    /// <summary>
    /// The strongly-relevant instructions file names (each including the
    /// <c>.instructions.md</c> extension), in corpus document order;
    /// disabled files excluded.
    /// </summary>
    [JsonPropertyName("instructions")]
    public IReadOnlyList<string> Instructions { get; init; } = [];

    /// <summary>
    /// The category names the prompt matched (canonical catalog case), in
    /// catalog document order; only categories that route to at least one
    /// tool.
    /// </summary>
    [JsonPropertyName("matchedCategories")]
    public IReadOnlyList<string> MatchedCategories { get; init; } = [];

    /// <summary>
    /// The file extensions the prompt named (each with a leading dot,
    /// e.g. <c>.cs</c>), in first-seen order; only extensions that map to
    /// at least one instructions file.
    /// </summary>
    [JsonPropertyName("matchedExtensions")]
    public IReadOnlyList<string> MatchedExtensions { get; init; } = [];

    /// <summary>
    /// The strongly-relevant MCP tool names, in catalog document order;
    /// disabled tools excluded.
    /// </summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<string> Tools { get; init; } = [];
}
