namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Result of the <see cref="InstructionsMethods.List"/> request: the
/// full listing as identity rows.
/// </summary>
public sealed record JsonInstructionsListResult
{
    /// <summary>
    /// Every bundled and override file as an identity row, including
    /// disabled files (which carry <c>disabled: true</c>).
    /// </summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<JsonInstructionsListRow> Files { get; init; } = [];
}
