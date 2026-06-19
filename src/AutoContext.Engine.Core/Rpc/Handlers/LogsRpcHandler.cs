namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Logs.*</c> handler. Serves a bounded read of the engine log file
/// (<c>Logs.GetEngine</c>) and tails live engine log records to subscribers
/// (<c>Logs.TailEngine</c>). Schema-invalid or out-of-range requests reply
/// <see cref="JsonRpcErrorCodes.InvalidParams"/>; a faulted read replies
/// <see cref="JsonRpcErrorCodes.InternalError"/>; in every case the
/// connection keeps serving.
/// </summary>
internal sealed partial class LogsRpcHandler : IRpcMethodHandler
{
    private readonly EngineLogFileReader _logFileReader;
    private readonly LogFrameStream _logFrameStream = new();
    private readonly ILogger<LogsRpcHandler> _logger;
    private readonly Broadcaster<JsonLogRecord> _logsBroadcaster;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogsRpcHandler"/>
    /// class.
    /// </summary>
    public LogsRpcHandler(
        EngineLogFileReader logFileReader,
        Broadcaster<JsonLogRecord> logsBroadcaster,
        ILogger<LogsRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logFileReader);
        ArgumentNullException.ThrowIfNull(logsBroadcaster);
        ArgumentNullException.ThrowIfNull(logger);

        _logFileReader = logFileReader;
        _logsBroadcaster = logsBroadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } =
        [LogsMethods.GetEngine, LogsMethods.TailEngine];

    /// <inheritdoc />
    public async ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Method switch
        {
            LogsMethods.TailEngine => HandleLogsTailEngine(),
            _ => await HandleLogsGetEngineAsync(request, cancellationToken).ConfigureAwait(false),
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Logs.GetEngine handler failed to read the engine log.")]
    private static partial void LogLogsGetEngineFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Logs.GetEngine rejected request with negative LastN={LastN}.")]
    private static partial void LogLogsGetEngineRejectedNegativeLastN(ILogger logger, int lastN);

    private async Task<RpcHandlerResult> HandleLogsGetEngineAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (RpcResults.TryDeserialize(
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

            return RpcResults.Success(result, ProtocolJsonContext.Default.JsonLogsGetEngineResult);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLogsGetEngineFailed(_logger, ex);
            return RpcResults.InternalError("Failed to read the engine log.");
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
        // up mid-stream or the iterator faults.
        var subscription = _logsBroadcaster.Subscribe();

        return new StreamingHandlerResult(
            Payloads: MapFramesAsync(subscription),
            PostFlush: () =>
            {
                subscription.Dispose();
                return Task.CompletedTask;
            });
    }

    private async IAsyncEnumerable<JsonElement> MapFramesAsync(
        BroadcasterSubscription<JsonLogRecord> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _logFrameStream
            .StreamAsync(subscription, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return JsonSerializer.SerializeToElement(
                frame, ProtocolJsonContext.Default.JsonLogStreamFrame);
        }
    }
}
