namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Terminal <see cref="JsonLogStreamFrame"/> arm the engine writes to
/// a <see cref="LogsMethods.TailWorker"/> subscriber when the
/// requested <c>workerId</c> is not a worker the current engine has
/// ever spawned. Mirrors the <c>not-found</c> arm of the unary
/// <see cref="JsonLogsGetWorkerResult"/> so a tailing client can tell
/// "no such worker" from a live-but-quiet worker's empty stream.
/// </summary>
/// <remarks>
/// Only <see cref="LogsMethods.TailWorker"/> ever emits this frame;
/// the <c>logs</c> pipe firehose and <see cref="LogsMethods.TailEngine"/>
/// never do (the engine's own records always have a home).
/// </remarks>
public sealed record JsonLogNotFoundFrame : JsonLogStreamFrame
{
    /// <summary>
    /// Creates a new <see cref="JsonLogNotFoundFrame"/>.
    /// </summary>
    /// <param name="workerId">The worker id that was requested.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="workerId"/> is <see langword="null"/> or empty.
    /// </exception>
    public JsonLogNotFoundFrame(string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        WorkerId = workerId;
    }

    /// <summary>The worker id that was requested.</summary>
    [JsonPropertyName("workerId")]
    public string WorkerId { get; init; }
}
