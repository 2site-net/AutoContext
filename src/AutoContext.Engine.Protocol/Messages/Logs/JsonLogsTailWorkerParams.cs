namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="LogsMethods.TailWorker"/> request.
/// Names the worker whose live records to stream. Unlike
/// <see cref="LogsMethods.TailEngine"/> (which takes no parameters),
/// the worker variant must be told which worker to filter the
/// shared record firehose down to.
/// </summary>
/// <remarks>
/// <see cref="WorkerId"/> is required — an absent or empty value is
/// rejected by the handler with <c>InvalidParams</c>. When the id
/// names a worker the engine has never spawned the stream yields a
/// single terminal <see cref="JsonLogNotFoundFrame"/> and completes.
/// </remarks>
public sealed record JsonLogsTailWorkerParams
{
    /// <summary>
    /// Identifier of the worker whose records to stream. Required;
    /// an absent or empty value is an <c>InvalidParams</c> error.
    /// </summary>
    [JsonPropertyName("workerId")]
    public string? WorkerId { get; init; }
}
