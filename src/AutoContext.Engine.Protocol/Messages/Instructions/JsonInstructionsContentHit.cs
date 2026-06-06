namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One ranked hit of the
/// <see cref="InstructionsMethods.SearchContent"/> response: a file
/// whose projected body matched the query, with up to a few excerpts.
/// </summary>
public sealed record JsonInstructionsContentHit
{
    /// <summary>The corpus file name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>File basename (the stable key).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>The corpus file name (<c>&lt;key&gt;.instructions.md</c>).</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    /// <summary>Trimmed frontmatter description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Relevance score; higher ranks first.</summary>
    [JsonPropertyName("score")]
    public double Score { get; init; }

    /// <summary>Matched excerpts with their section anchors.</summary>
    [JsonPropertyName("excerpts")]
    public IReadOnlyList<JsonInstructionsContentExcerpt> Excerpts { get; init; } = [];
}
