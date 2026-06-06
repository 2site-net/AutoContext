namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>ok</c> arm of <see cref="JsonInstructionsGetResult"/>:
/// the requested file exists, is active, and its projected body was
/// read successfully.
/// </summary>
public sealed record JsonInstructionsGetOkResult : JsonInstructionsGetResult
{
    /// <summary>The corpus file name that was read.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>File basename (the stable key).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>The corpus file name (<c>&lt;key&gt;.instructions.md</c>).</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    /// <summary>
    /// Projected body — disabled rules filtered, <c>[INSTxxxx]</c>
    /// tags stripped, override preferred over bundled — sliced to the
    /// requested sections when
    /// <see cref="JsonInstructionsGetParams.Sections"/> was set.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// Anchors actually included in <see cref="Content"/>. Equals the
    /// full section set when the request did not slice.
    /// </summary>
    [JsonPropertyName("returnedSections")]
    public IReadOnlyList<string> ReturnedSections { get; init; } = [];

    /// <summary>
    /// Requested anchors that were not found, or <see langword="null"/>
    /// when the request did not slice or every anchor resolved.
    /// </summary>
    [JsonPropertyName("notFoundSections")]
    public IReadOnlyList<string>? NotFoundSections { get; init; }
}
