namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// <see cref="JsonInstructionsStreamFrame"/> arm carrying the current
/// corpus catalogue as identity rows — the snapshot-on-subscribe seed
/// for every new subscriber and again on every corpus reload.
/// </summary>
public sealed record JsonInstructionsSnapshotFrame : JsonInstructionsStreamFrame
{
    /// <summary>
    /// Creates a new <see cref="JsonInstructionsSnapshotFrame"/>.
    /// </summary>
    /// <param name="files">Catalogue rows to carry on the wire.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="files"/> is <see langword="null"/>.
    /// </exception>
    public JsonInstructionsSnapshotFrame(IReadOnlyList<JsonInstructionsListRow> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Files = files;
    }

    /// <summary>
    /// The catalogue rows. Mirrors a
    /// <see cref="JsonInstructionsListResult.Files"/> payload.
    /// </summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<JsonInstructionsListRow> Files { get; init; }
}
