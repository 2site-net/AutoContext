namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="InstructionsMethods.List"/> request.
/// </summary>
public sealed record JsonInstructionsListParams
{
    /// <summary>
    /// Whether each row carries its
    /// <see cref="JsonInstructionsListRow.Sections"/> index.
    /// <see langword="null"/> (the default when the param is omitted)
    /// is treated as <see langword="true"/> — the LM-tool and
    /// discovery paths need sections; tree-view callers pass
    /// <see langword="false"/> to drop the payload.
    /// </summary>
    [JsonPropertyName("includeSections")]
    public bool? IncludeSections { get; init; }
}
