namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// <see cref="JsonLogStreamFrame"/> arm carrying one
/// <see cref="JsonLogRecord"/> drained from the engine's log pipeline.
/// </summary>
public sealed record JsonLogRecordFrame : JsonLogStreamFrame
{
    /// <summary>
    /// Creates a new <see cref="JsonLogRecordFrame"/>.
    /// </summary>
    /// <param name="record">Log record to carry on the wire.</param>
    public JsonLogRecordFrame(JsonLogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
    }

    /// <summary>
    /// The wrapped <see cref="JsonLogRecord"/>. Serialised as a nested
    /// JSON object on the <c>logs</c>-pipe wire.
    /// </summary>
    [JsonPropertyName("record")]
    public JsonLogRecord Record { get; init; }
}
