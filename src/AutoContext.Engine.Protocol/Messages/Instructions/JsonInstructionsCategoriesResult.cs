namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Result of the <see cref="InstructionsMethods.Categories"/> request:
/// the curated category taxonomy (bucket definitions) hand-authored in
/// <c>instructions-catalog.json</c>. Static for the engine's process
/// lifetime, so clients fetch it once and cache it — the per-file
/// <see cref="JsonInstructionsListRow.Category"/> membership string
/// on <see cref="InstructionsMethods.List"/> rows resolves against these
/// definitions.
/// </summary>
public sealed record JsonInstructionsCategoriesResult
{
    /// <summary>The category definitions, in deterministic order.</summary>
    [JsonPropertyName("categories")]
    public IReadOnlyList<JsonInstructionsCategory> Categories { get; init; } = [];
}
