namespace AutoContext.Engine.Protocol.Messages.Discovery;

using System.Text.Json.Serialization;

/// <summary>
/// Request params for <see cref="DiscoveryMethods.RouteForPrompt"/>: the
/// raw user prompt the engine scans for category words and file
/// extensions. A <see langword="null"/> or empty prompt yields an empty
/// route.
/// </summary>
public sealed record JsonDiscoveryRouteForPromptParams
{
    /// <summary>The user prompt to route.</summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }
}
