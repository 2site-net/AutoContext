namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Result of the <see cref="InstructionsMethods.GetAll"/> and
/// <see cref="InstructionsMethods.GetAlwaysAttached"/> requests: a
/// bulk list of projected file bodies. Both methods exclude disabled
/// files — they never return a disabled identity envelope.
/// </summary>
public sealed record JsonInstructionsFilesResult
{
    /// <summary>The projected files, in deterministic order.</summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<JsonInstructionsFile> Files { get; init; } = [];
}
