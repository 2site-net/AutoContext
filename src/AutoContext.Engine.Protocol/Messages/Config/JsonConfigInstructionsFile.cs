namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of one instruction file's config state, carried on
/// <see cref="JsonConfigSnapshot.Instructions"/>.
/// </summary>
public sealed record JsonConfigInstructionsFile
{
    /// <summary>
    /// File key (basename), or <see langword="null"/> when unknown.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Version the override was recorded against, or
    /// <see langword="null"/> when unset.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Whether the whole file is disabled. <see langword="null"/>
    /// means "not toggled" (enabled).
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    /// <summary>
    /// Per-rule overrides within this file. Empty when no rule is
    /// individually toggled.
    /// </summary>
    [JsonPropertyName("rules")]
    public IReadOnlyList<JsonConfigInstructionsRule> Rules { get; init; } = [];
}
