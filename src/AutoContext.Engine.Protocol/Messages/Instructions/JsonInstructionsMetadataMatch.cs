namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One matched file of <see cref="JsonInstructionsSearchByMetadataOkResult"/>:
/// the identity <see cref="File"/> row (the same shape
/// <see cref="InstructionsMethods.List"/> returns) plus the
/// <see cref="MatchedAnchors"/> of the sections that satisfied a
/// <c>sections.*</c> clause — the anchors a chained
/// <see cref="InstructionsMethods.Get"/> can slice by.
/// </summary>
public sealed record JsonInstructionsMetadataMatch
{
    /// <summary>The matched file's identity row.</summary>
    [JsonPropertyName("file")]
    public required JsonInstructionsListRow File { get; init; }

    /// <summary>
    /// The anchors of the sections that satisfied every <c>sections.*</c>
    /// clause, or <see langword="null"/> when the predicate named no
    /// <c>sections.*</c> field.
    /// </summary>
    [JsonPropertyName("matchedAnchors")]
    public IReadOnlyList<string>? MatchedAnchors { get; init; }
}
