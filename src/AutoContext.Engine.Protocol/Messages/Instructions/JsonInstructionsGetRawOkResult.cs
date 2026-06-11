namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>ok</c> arm of <see cref="JsonInstructionsGetRawResult"/>:
/// the source-faithful bytes of the resolved on-disk markdown file —
/// frontmatter and <c>[INSTxxxx]</c> tags intact.
/// </summary>
public sealed record JsonInstructionsGetRawOkResult : JsonInstructionsGetRawResult
{
    /// <summary>The corpus file name that was read.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>File basename (the stable key).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>
    /// The concrete file the bytes came from — never the
    /// <c>active</c> selector. Lets a caller that passed
    /// <see cref="InstructionsRawSource.Active"/> learn which file
    /// actually resolved.
    /// </summary>
    [JsonPropertyName("source")]
    public InstructionsSource Source { get; init; }

    /// <summary>The unmodified file bytes, decoded as UTF-8.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
