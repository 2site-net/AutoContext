namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>not-found</c> arm of
/// <see cref="JsonInstructionsGetRawResult"/>: no file resolved for
/// the requested name and source — e.g. <c>source: "override"</c> with
/// no override present, or a name absent from the corpus.
/// </summary>
public sealed record JsonInstructionsGetRawNotFoundResult : JsonInstructionsGetRawResult
{
    /// <summary>The corpus file name that was requested.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
