namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// <see cref="JsonConfigStreamFrame"/> arm carrying one full
/// <see cref="JsonConfigSnapshot"/> — the engine's current config
/// state. Emitted as the snapshot-on-subscribe seed for every new
/// subscriber and again on every subsequent config change.
/// </summary>
public sealed record JsonConfigSnapshotFrame : JsonConfigStreamFrame
{
    /// <summary>
    /// Creates a new <see cref="JsonConfigSnapshotFrame"/>.
    /// </summary>
    /// <param name="snapshot">Config snapshot to carry on the wire.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshot"/> is <see langword="null"/>.
    /// </exception>
    public JsonConfigSnapshotFrame(JsonConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
    }

    /// <summary>
    /// The wrapped <see cref="JsonConfigSnapshot"/>. Serialised as a
    /// nested JSON object on the <c>Config.Subscribe</c> stream wire.
    /// </summary>
    [JsonPropertyName("snapshot")]
    public JsonConfigSnapshot Snapshot { get; init; }
}
