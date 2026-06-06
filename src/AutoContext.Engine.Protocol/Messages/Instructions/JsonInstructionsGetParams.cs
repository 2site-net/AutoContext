namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="InstructionsMethods.Get"/> request.
/// </summary>
public sealed record JsonInstructionsGetParams
{
    /// <summary>The corpus file name to read. Required.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Optional section anchors to slice the projected body down to.
    /// When omitted, the whole projected body is returned. Requested
    /// anchors that exist surface in
    /// <see cref="JsonInstructionsGetOkResult.ReturnedSections"/>;
    /// those that do not surface in
    /// <see cref="JsonInstructionsGetOkResult.NotFoundSections"/>.
    /// </summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<string>? Sections { get; init; }
}
