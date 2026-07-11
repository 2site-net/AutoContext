namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>not-found</c> arm of <see cref="JsonLogsGetWorkerResult"/>:
/// the requested <c>workerId</c> is not a worker the current engine
/// has ever spawned — strictly distinct from an <c>ok</c> result
/// with an empty <c>records</c> array (a spawned worker that has
/// not logged yet).
/// </summary>
public sealed record JsonLogsGetWorkerNotFoundResult : JsonLogsGetWorkerResult
{
    /// <summary>The worker id that was requested.</summary>
    [JsonPropertyName("workerId")]
    public string? WorkerId { get; init; }
}
