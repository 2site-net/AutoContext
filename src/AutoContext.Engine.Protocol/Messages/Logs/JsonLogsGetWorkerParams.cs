namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="LogsMethods.GetWorker"/> request.
/// Names the worker whose <c>worker-&lt;workerId&gt;.log</c> to read
/// and carries the same optional snapshot bounds as
/// <see cref="JsonLogsGetEngineParams"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WorkerId"/> is required — unlike the engine variant,
/// there is no "the current process" default to fall back on. An
/// absent or empty id is rejected by the handler with
/// <c>InvalidParams</c> before the read is attempted.
/// </para>
/// <para>
/// <see cref="LastN"/> and <see cref="Since"/> follow the same
/// independent-filter semantics as
/// <see cref="JsonLogsGetEngineParams"/>: <see cref="Since"/> is
/// applied first (timestamp filter), then <see cref="LastN"/> caps
/// from the tail.
/// </para>
/// </remarks>
public sealed record JsonLogsGetWorkerParams
{
    /// <summary>
    /// Identifier of the worker whose log file to read. Required;
    /// an absent or empty value is an <c>InvalidParams</c> error.
    /// </summary>
    [JsonPropertyName("workerId")]
    public string? WorkerId { get; init; }

    /// <summary>
    /// Maximum number of records to return, counted from the tail
    /// of the worker's active file. <see langword="null"/> means no
    /// cap. See <see cref="JsonLogsGetEngineParams.LastN"/>.
    /// </summary>
    [JsonPropertyName("lastN")]
    public int? LastN { get; init; }

    /// <summary>
    /// Inclusive lower bound on <see cref="JsonLogRecord.Timestamp"/>.
    /// Records strictly older than this value are excluded.
    /// <see langword="null"/> means "no lower bound".
    /// </summary>
    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; init; }
}
