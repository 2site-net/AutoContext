namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// <see cref="LogStreamFrame"/> arm carrying one
/// <see cref="LogRecord"/> drained from the engine's log pipeline.
/// </summary>
public sealed record LogRecordFrame : LogStreamFrame
{
    /// <summary>
    /// Creates a new <see cref="LogRecordFrame"/>.
    /// </summary>
    /// <param name="record">Log record to carry on the wire.</param>
    public LogRecordFrame(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
    }

    /// <summary>
    /// The wrapped <see cref="LogRecord"/>. Serialised as a nested
    /// JSON object on the <c>logs</c>-pipe wire.
    /// </summary>
    [JsonPropertyName("record")]
    public LogRecord Record { get; init; }
}
