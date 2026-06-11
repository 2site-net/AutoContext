namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One curated category (bucket definition) of the
/// <see cref="InstructionsMethods.Categories"/> taxonomy — a
/// <c>name</c> plus its human-readable <c>description</c>. These
/// definitions are hand-authored in <c>instructions-catalog.json</c>;
/// the per-file membership strings on
/// <see cref="JsonInstructionsListRow.Categories"/> resolve to them.
/// </summary>
public sealed record JsonInstructionsCategory
{
    /// <summary>The category name (the membership key a row references).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The human-readable category description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
