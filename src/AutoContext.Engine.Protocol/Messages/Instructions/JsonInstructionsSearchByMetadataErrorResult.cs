namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>error</c> arm of <see cref="JsonInstructionsSearchByMetadataResult"/>:
/// a structured predicate fault. <see cref="Error"/> is the fault code
/// (<c>unknown-field</c> / <c>type-mismatch</c> / <c>invalid-regex</c> /
/// <c>pattern-too-long</c>), <see cref="Field"/> is the offending predicate
/// key, and <see cref="RecognizedFields"/> lists every field the engine
/// accepts so the model caller can correct the predicate in one step.
/// </summary>
public sealed record JsonInstructionsSearchByMetadataErrorResult
    : JsonInstructionsSearchByMetadataResult
{
    /// <summary>
    /// The fault code: <c>unknown-field</c>, <c>type-mismatch</c>,
    /// <c>invalid-regex</c>, or <c>pattern-too-long</c>.
    /// </summary>
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    /// <summary>The offending predicate field name.</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    /// <summary>A human-readable explanation of the fault.</summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    /// <summary>
    /// The self-describing schema of every recognised predicate field, so the
    /// model caller can correct an invalid predicate without a second lookup.
    /// </summary>
    [JsonPropertyName("recognizedFields")]
    public IReadOnlyList<JsonInstructionsMetadataFieldInfo> RecognizedFields { get; init; } = [];
}
