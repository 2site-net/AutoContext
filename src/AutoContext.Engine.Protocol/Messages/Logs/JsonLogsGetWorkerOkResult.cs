namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>ok</c> arm of <see cref="JsonLogsGetWorkerResult"/>: the
/// requested worker was spawned by the current engine, and its
/// bounded log snapshot was read successfully (possibly empty when
/// the worker has not logged yet).
/// </summary>
public sealed record JsonLogsGetWorkerOkResult : JsonLogsGetWorkerResult
{
    /// <summary>
    /// The records satisfying the request's filter, in
    /// chronological order (oldest first), bounded by
    /// <see cref="JsonLogsGetWorkerParams.LastN"/> when present.
    /// Empty when the worker has produced no matching record.
    /// </summary>
    [JsonPropertyName("records")]
    public IReadOnlyList<JsonLogRecord> Records { get; init; } = [];

    /// <summary>
    /// <see langword="true"/> when the worker's active file rolled
    /// past part of the requested range. Same semantics as
    /// <see cref="JsonLogsGetEngineResult.Truncated"/>.
    /// </summary>
    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }
}
