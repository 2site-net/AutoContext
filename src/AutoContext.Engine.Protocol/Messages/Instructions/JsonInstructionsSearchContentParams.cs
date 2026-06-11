namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="InstructionsMethods.SearchContent"/>
/// request.
/// </summary>
public sealed record JsonInstructionsSearchContentParams
{
    /// <summary>The search query. Required.</summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>
    /// Maximum number of hits to return, or <see langword="null"/> for
    /// the engine default.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>
    /// Whether disabled files participate in the search.
    /// <see langword="null"/> (the default) excludes them; the LM tool
    /// flips this on only when explicitly asked to surface disabled
    /// guidance.
    /// </summary>
    [JsonPropertyName("includeDisabled")]
    public bool? IncludeDisabled { get; init; }
}
