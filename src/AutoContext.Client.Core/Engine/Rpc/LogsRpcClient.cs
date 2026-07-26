namespace AutoContext.Client.Core.Engine.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's unary <c>Logs.*</c> read RPCs over a
/// live <see cref="EngineConnection"/>. Reads a bounded snapshot of the
/// engine's active <c>engine.log</c> or a worker's
/// <c>worker-&lt;workerId&gt;.log</c>; the live tail is a subscription
/// consumer (<c>Subscriptions.LogsTailSubscription</c>), not part of
/// this unary surface. <see cref="GetWorkerAsync"/> returns the
/// discriminated <see cref="JsonLogsGetWorkerResult"/> so a
/// never-spawned worker surfaces as <c>not-found</c> rather than a
/// nullable.
/// </summary>
public sealed class LogsRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="LogsRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public LogsRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Reads a bounded snapshot of the engine's active <c>engine.log</c>
    /// file, optionally capped to the last <paramref name="lastN"/>
    /// records and/or filtered to timestamps at or after
    /// <paramref name="since"/>.
    /// </summary>
    /// <param name="lastN">Tail cap, or <see langword="null"/> for no
    /// cap.</param>
    /// <param name="since">Inclusive lower timestamp bound, or
    /// <see langword="null"/> for no bound.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonLogsGetEngineResult> GetEngineAsync(
        int? lastN, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.SerializeToElement(
            new JsonLogsGetEngineParams { LastN = lastN, Since = since },
            ProtocolJsonContext.Default.JsonLogsGetEngineParams);

        return _connection.InvokeAsync(
            LogsMethods.GetEngine,
            parameters,
            ProtocolJsonContext.Default.JsonLogsGetEngineResult,
            cancellationToken);
    }

    /// <summary>
    /// Reads a bounded snapshot of the worker
    /// <paramref name="workerId"/>'s active log file, returning the
    /// discriminated result — <c>ok</c> when the worker was spawned by
    /// this engine (even if it has not logged yet), <c>not-found</c>
    /// when it never was.
    /// </summary>
    /// <param name="workerId">Worker whose log to read. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="lastN">Tail cap, or <see langword="null"/> for no
    /// cap.</param>
    /// <param name="since">Inclusive lower timestamp bound, or
    /// <see langword="null"/> for no bound.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonLogsGetWorkerResult> GetWorkerAsync(
        string workerId, int? lastN, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonLogsGetWorkerParams { WorkerId = workerId, LastN = lastN, Since = since },
            ProtocolJsonContext.Default.JsonLogsGetWorkerParams);

        return _connection.InvokeAsync(
            LogsMethods.GetWorker,
            parameters,
            ProtocolJsonContext.Default.JsonLogsGetWorkerResult,
            cancellationToken);
    }
}
