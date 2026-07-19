namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One row of the recognised-field schema returned with every
/// <see cref="JsonInstructionsSearchByMetadataErrorResult"/>. <see cref="Type"/>
/// is the JSON value type expected for the predicate value (<c>string</c> /
/// <c>number</c> / <c>boolean</c>); <see cref="Match"/> is how the value is
/// interpreted (<c>regex</c> — case-insensitive regular expression; <c>glob</c>
/// — workspace glob, coarse extension intersection; <c>equality</c> — exact
/// value equality).
/// </summary>
public sealed record JsonInstructionsMetadataFieldInfo
{
    /// <summary>The recognised predicate field name.</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    /// <summary>
    /// The JSON value type the predicate value must have: <c>string</c>,
    /// <c>number</c>, or <c>boolean</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// How the value is interpreted: <c>regex</c>, <c>glob</c>, or
    /// <c>equality</c>.
    /// </summary>
    [JsonPropertyName("match")]
    public required string Match { get; init; }
}
