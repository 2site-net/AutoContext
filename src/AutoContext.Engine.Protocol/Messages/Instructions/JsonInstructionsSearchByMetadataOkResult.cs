namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>ok</c> arm of <see cref="JsonInstructionsSearchByMetadataResult"/>:
/// the files whose metadata satisfied the predicate, each paired with the
/// section anchors that matched a <c>sections.*</c> clause. Disabled files are
/// omitted (metadata search surfaces discoverable, active guidance only).
/// </summary>
public sealed record JsonInstructionsSearchByMetadataOkResult
    : JsonInstructionsSearchByMetadataResult
{
    /// <summary>The matched files, in corpus order.</summary>
    [JsonPropertyName("results")]
    public IReadOnlyList<JsonInstructionsMetadataMatch> Results { get; init; } = [];
}
