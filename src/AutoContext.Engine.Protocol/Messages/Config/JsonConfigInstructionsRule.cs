namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of one instruction rule's config state, carried on
/// <see cref="JsonConfigInstructionsFile.Rules"/>.
/// </summary>
public sealed record JsonConfigInstructionsRule
{
    /// <summary>
    /// Stable rule id, or <see langword="null"/> when the rule has
    /// none.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Whether the rule is disabled. <see langword="null"/> means
    /// "not toggled" (enabled).
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }
}
