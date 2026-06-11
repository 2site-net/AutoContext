namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="InstructionsMethods.GetRaw"/> request.
/// </summary>
public sealed record JsonInstructionsGetRawParams
{
    /// <summary>The corpus file name to read. Required.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Override-resolution selector. Defaults to
    /// <see cref="InstructionsRawSource.Active"/> when omitted.
    /// Callers whose byte offsets must align with a specific on-disk
    /// file (CodeLens, "open instruction source") pass
    /// <see cref="InstructionsRawSource.Bundled"/> or
    /// <see cref="InstructionsRawSource.Override"/> explicitly.
    /// </summary>
    [JsonPropertyName("source")]
    public InstructionsRawSource Source { get; init; }
}
