namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Result of the <see cref="InstructionsMethods.SearchContent"/>
/// request: the ranked content hits, highest score first.
/// </summary>
public sealed record JsonInstructionsSearchContentResult
{
    /// <summary>The ranked hits.</summary>
    [JsonPropertyName("hits")]
    public IReadOnlyList<JsonInstructionsContentHit> Hits { get; init; } = [];
}
