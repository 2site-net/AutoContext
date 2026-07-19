namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="InstructionsMethods.SearchByMetadata"/>
/// request. The <see cref="Predicate"/> is a free-form object whose keys are
/// metadata field names and whose values are the patterns/values to match; the
/// engine validates field names and value kinds and returns a structured error
/// envelope (never throws) so the model caller can correct an invalid
/// predicate. An absent or empty predicate matches every file.
/// </summary>
public sealed record JsonInstructionsSearchByMetadataParams
{
    /// <summary>
    /// The metadata predicate object — a map of field name to matched
    /// value. String fields are matched by case-insensitive regex,
    /// <c>applyTo</c> by workspace glob (coarse extension intersection),
    /// <c>hasChangelog</c> by boolean equality, and <c>sections.level</c> by
    /// numeric equality. <see langword="null"/> or an empty object matches
    /// every file.
    /// </summary>
    [JsonPropertyName("predicate")]
    public JsonElement? Predicate { get; init; }

    /// <summary>
    /// Whether each matched row carries its section index. Independent of the
    /// <c>matchedAnchors</c> a <c>sections.*</c> clause always reports.
    /// <see langword="null"/> is treated as <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("includeSections")]
    public bool? IncludeSections { get; init; }
}
