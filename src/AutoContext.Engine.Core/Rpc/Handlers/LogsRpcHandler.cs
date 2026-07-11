namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Workers;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Logs.*</c> handler. Serves bounded reads of the engine log
/// (<c>Logs.GetEngine</c>) and a worker's log (<c>Logs.GetWorker</c>),
/// and tails the live record stream filtered to the engine's own records
/// (<c>Logs.TailEngine</c>) or a single worker's records
/// (<c>Logs.TailWorker</c>). The worker variants return the
/// <c>not-found</c> arm for a worker the engine has never spawned.
/// Schema-invalid or out-of-range requests reply
/// <see cref="JsonRpcErrorCodes.InvalidParams"/>; a faulted read replies
/// <see cref="JsonRpcErrorCodes.InternalError"/>; in every case the
/// connection keeps serving.
/// </summary>
internal sealed partial class LogsRpcHandler : IRpcMethodHandler
{
    private readonly LogFileReader _logFileReader;
    private readonly LogFrameStream _logFrameStream = new();
    private readonly ILogger<LogsRpcHandler> _logger;
    private readonly Broadcaster<JsonLogRecord> _logsBroadcaster;
    private readonly IWorkerSpawnTracker _workerSpawnTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogsRpcHandler"/>
    /// class.
    /// </summary>
    public LogsRpcHandler(
        LogFileReader logFileReader,
        Broadcaster<JsonLogRecord> logsBroadcaster,
        IWorkerSpawnTracker workerSpawnTracker,
        ILogger<LogsRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logFileReader);
        ArgumentNullException.ThrowIfNull(logsBroadcaster);
        ArgumentNullException.ThrowIfNull(workerSpawnTracker);
        ArgumentNullException.ThrowIfNull(logger);

        _logFileReader = logFileReader;
        _logsBroadcaster = logsBroadcaster;
        _workerSpawnTracker = workerSpawnTracker;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } =
        [LogsMethods.GetEngine, LogsMethods.TailEngine, LogsMethods.GetWorker, LogsMethods.TailWorker];

    /// <inheritdoc />
    public async ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Method switch
        {
            LogsMethods.TailEngine => HandleLogsTailEngine(),
            LogsMethods.GetWorker => await HandleLogsGetWorkerAsync(request, cancellationToken).ConfigureAwait(false),
            LogsMethods.TailWorker => HandleLogsTailWorker(request),
            _ => await HandleLogsGetEngineAsync(request, cancellationToken).ConfigureAwait(false),
        };
    }

    private static UnaryHandlerResult GetWorkerResult(JsonLogsGetWorkerResult result)
        => RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonLogsGetWorkerResult);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Logs.GetEngine handler failed to read the engine log.")]
    private static partial void LogLogsGetEngineFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Logs.GetEngine rejected request with negative LastN={LastN}.")]
    private static partial void LogLogsGetEngineRejectedNegativeLastN(ILogger logger, int lastN);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Logs.GetWorker handler failed to read the log for worker '{WorkerId}'.")]
    private static partial void LogLogsGetWorkerFailed(ILogger logger, string workerId, Exception exception);

    private static async IAsyncEnumerable<JsonElement> NotFoundFramesAsync(string workerId)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield return JsonSerializer.SerializeToElement(
            new JsonLogNotFoundFrame(workerId),
            ProtocolJsonContext.Default.JsonLogStreamFrame);
    }

    private async Task<RpcHandlerResult> HandleLogsGetEngineAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (RpcMethodResults.TryDeserialize(
                request,
                LogsMethods.GetEngine,
                ProtocolJsonContext.Default.JsonLogsGetEngineParams,
                _logger,
                out var parameters) is { } failure)
        {
            return failure;
        }

        if (parameters?.LastN is < 0)
        {
            LogLogsGetEngineRejectedNegativeLastN(_logger, parameters.LastN.GetValueOrDefault());
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InvalidParams,
                        Message = "LastN must be non-negative.",
                    },
                },
                Continuation: Continuation.Continue);
        }

        try
        {
            var read = await _logFileReader.ReadAsync(parameters, cancellationToken)
                .ConfigureAwait(false);

            var result = new JsonLogsGetEngineResult
            {
                Records = read.Records,
                Truncated = read.Truncated,
            };

            return RpcMethodResults.Success(result, ProtocolJsonContext.Default.JsonLogsGetEngineResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLogsGetEngineFailed(_logger, ex);
            return RpcMethodResults.InternalError("Failed to read the engine log.");
        }
    }

    private async Task<RpcHandlerResult> HandleLogsGetWorkerAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (RpcMethodResults.TryDeserialize(
                request,
                LogsMethods.GetWorker,
                ProtocolJsonContext.Default.JsonLogsGetWorkerParams,
                _logger,
                out var parameters) is { } failure)
        {
            return failure;
        }

        var workerId = parameters?.WorkerId;

        if (string.IsNullOrEmpty(workerId))
        {
            return RpcMethodResults.InvalidParams(LogsMethods.GetWorker);
        }

        if (parameters?.LastN is < 0)
        {
            return RpcMethodResults.InvalidParams(LogsMethods.GetWorker);
        }

        if (!_workerSpawnTracker.HasEverSpawned(workerId))
        {
            return GetWorkerResult(new JsonLogsGetWorkerNotFoundResult { WorkerId = workerId });
        }

        try
        {
            var read = await _logFileReader.ReadWorkerAsync(workerId, parameters, cancellationToken)
                .ConfigureAwait(false);

            var result = new JsonLogsGetWorkerOkResult
            {
                Records = read.Records,
                Truncated = read.Truncated,
            };

            return GetWorkerResult(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLogsGetWorkerFailed(_logger, workerId, ex);
            return RpcMethodResults.InternalError("Failed to read the worker log.");
        }
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership of the subscription is handed off to StreamingHandlerResult.PostFlush, which the RpcConnectionProcessor runs in a finally block — disposal is guaranteed on every path.")]
    private StreamingHandlerResult HandleLogsTailEngine()
    {
        // Subscription is created up-front so its disposal can be
        // routed through StreamingHandlerResult.PostFlush, which
        // the processor runs in a finally — guaranteeing the
        // broadcaster slot is released even when the peer hangs
        // up mid-stream or the iterator faults. TailEngine serves the
        // engine's own records only: worker records (which route to a
        // worker-<id>.log) are filtered out of the engine feed.
        var subscription = _logsBroadcaster.Subscribe();

        return new StreamingHandlerResult(
            Payloads: MapFramesAsync(
                subscription,
                static category => !LogFileSinkService.TryExtractWorkerId(category, out _)),
            PostFlush: () =>
            {
                subscription.Dispose();
                return Task.CompletedTask;
            });
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership of the subscription is handed off to StreamingHandlerResult.PostFlush, which the RpcConnectionProcessor runs in a finally block — disposal is guaranteed on every path. The not-found arm creates no subscription.")]
    private RpcHandlerResult HandleLogsTailWorker(JsonRpcRequest request)
    {
        if (RpcMethodResults.TryDeserialize(
                request,
                LogsMethods.TailWorker,
                ProtocolJsonContext.Default.JsonLogsTailWorkerParams,
                _logger,
                out var parameters) is { } failure)
        {
            return failure;
        }

        var workerId = parameters?.WorkerId;

        if (string.IsNullOrEmpty(workerId))
        {
            return RpcMethodResults.InvalidParams(LogsMethods.TailWorker);
        }

        if (!_workerSpawnTracker.HasEverSpawned(workerId))
        {
            // A worker this engine never spawned yields a single
            // terminal not-found frame and completes — no subscription
            // to enrol or dispose.
            return new StreamingHandlerResult(
                Payloads: NotFoundFramesAsync(workerId),
                PostFlush: null);
        }

        var subscription = _logsBroadcaster.Subscribe();

        return new StreamingHandlerResult(
            Payloads: MapFramesAsync(
                subscription,
                category => LogFileSinkService.TryExtractWorkerId(category, out var id)
                    && string.Equals(id, workerId, StringComparison.Ordinal)),
            PostFlush: () =>
            {
                subscription.Dispose();
                return Task.CompletedTask;
            });
    }

    private async IAsyncEnumerable<JsonElement> MapFramesAsync(
        BroadcasterSubscription<JsonLogRecord> subscription,
        Func<string, bool> includeCategory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _logFrameStream
            .StreamAsync(subscription, cancellationToken)
            .ConfigureAwait(false))
        {
            // Terminal frames (dropped / not-found) carry no category
            // and always pass; record frames are filtered to the feed
            // this stream serves.
            if (frame is JsonLogRecordFrame recordFrame
                && !includeCategory(recordFrame.Record.Category))
            {
                continue;
            }

            yield return JsonSerializer.SerializeToElement(
                frame, ProtocolJsonContext.Default.JsonLogStreamFrame);
        }
    }
}
