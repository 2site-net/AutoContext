namespace AutoContext.Engine.Core.Rpc.Policies;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Logging.Primitives;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IRpcConnectionPolicy"/> that runs after a successful
/// <c>Engine.Hello</c> handshake on an
/// <see cref="EndpointKind.Rpc"/> connection. Per
/// <c>design § RPC surface</c> the engine currently exposes two
/// methods — <c>Engine.RegistryEntries</c> and
/// <c>Engine.Shutdown</c>; any other method name surfaces a
/// JSON-RPC <see cref="JsonRpcErrorCodes.MethodNotFound"/> reply
/// and the loop keeps serving.
/// </summary>
/// <remarks>
/// <para>
/// The policy is intentionally narrow: it does not multiplex
/// concurrent handlers on one connection and it does not interpret
/// <c>Engine.Hello</c> — the handshake step owns that. Recoverable
/// per-frame failures (malformed JSON, unknown method) reply with
/// the appropriate error code and the processor keeps reading
/// (<see cref="FrameFailurePolicy.Recover"/>).
/// </para>
/// <para>
/// <c>Engine.Shutdown</c> returns <c>{ accepted: true }</c> with a
/// <see cref="Continuation.Complete"/> continuation and a
/// <see cref="RpcHandlerResult.PostFlush"/> that calls
/// <see cref="IHostApplicationLifetime.StopApplication"/>. The
/// processor guarantees the response lands on the wire before the
/// post-flush runs — so the host begins tearing down listeners only
/// after the acknowledgement has been observed by the client. The
/// hosted-service stop sequence (which runs in reverse-registration
/// order) then drains and disposes the four pipes.
/// </para>
/// </remarks>
internal sealed partial class DispatchPolicy : IRpcConnectionPolicy
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly RegistryFileReader _registryReader;
    private readonly EngineLogFileReader _logFileReader;
    private readonly LogSubscriptionBroadcaster _logsBroadcaster;
    private readonly ILogger _logger;

    public DispatchPolicy(
        IHostApplicationLifetime lifetime,
        RegistryFileReader registryReader,
        EngineLogFileReader logFileReader,
        LogSubscriptionBroadcaster logsBroadcaster,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(registryReader);
        ArgumentNullException.ThrowIfNull(logFileReader);
        ArgumentNullException.ThrowIfNull(logsBroadcaster);
        ArgumentNullException.ThrowIfNull(logger);

        _lifetime = lifetime;
        _registryReader = registryReader;
        _logFileReader = logFileReader;
        _logsBroadcaster = logsBroadcaster;
        _logger = logger;
    }

    public EndpointKind EndpointKind => EndpointKind.Rpc;

    public FrameFailurePolicy FrameFailurePolicy => FrameFailurePolicy.Recover;

    public void LogFrameReadFault(Exception exception) =>
        LogReadFaulted(_logger, exception);

    public void LogFrameWriteFault(Exception exception) =>
        LogWriteFaulted(_logger, exception);

    public void LogFrameParseFault(Exception exception) =>
        LogFrameParseFailed(_logger, exception);

    public void LogFrameInvalidRequest() =>
        LogInvalidRequest(_logger);

    public void LogConnectionClosedByPeer()
    {
        // Quiet by design: a post-handshake client disconnecting
        // cleanly between requests is normal behaviour, not a
        // diagnostic event worth recording.
    }

    public async ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request.Method)
        {
            case RegistryMethods.RegistryEntries:
                return await HandleRegistryEntriesAsync(cancellationToken)
                    .ConfigureAwait(false);

            case LogsMethods.GetEngine:
                return await HandleLogsGetEngineAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case LogsMethods.TailEngine:
                return HandleLogsTailEngine();

            case ProtocolMethods.Shutdown:
                return HandleShutdown();

            default:
                LogMethodNotFound(_logger, request.Method);
                return new UnaryHandlerResult(
                    Response: new JsonRpcResponse
                    {
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.MethodNotFound,
                            Message = $"Unknown method '{request.Method}'.",
                        },
                    },
                    Continuation: Continuation.Continue);
        }
    }

    private async Task<RpcHandlerResult> HandleRegistryEntriesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _registryReader.ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = new RegistryEntriesResult { Entries = entries };
            var resultElement = JsonSerializer.SerializeToElement(
                result, ProtocolJsonContext.Default.RegistryEntriesResult);

            return new UnaryHandlerResult(
                Response: new JsonRpcResponse { Result = resultElement },
                Continuation: Continuation.Continue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRegistryEntriesFailed(_logger, ex);
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = "Failed to read the engine registry.",
                    },
                },
                Continuation: Continuation.Continue);
        }
    }

    private async Task<RpcHandlerResult> HandleLogsGetEngineAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        LogsGetEngineParams? parameters;

        try
        {
            parameters = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(ProtocolJsonContext.Default.LogsGetEngineParams)
                : null;
        }
        catch (JsonException ex)
        {
            LogParamsParseFailed(_logger, LogsMethods.GetEngine, ex);
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InvalidParams,
                        Message = $"Invalid params for '{LogsMethods.GetEngine}'.",
                    },
                },
                Continuation: Continuation.Continue);
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

            var result = new LogsGetEngineResult
            {
                Records = read.Records,
                Truncated = read.Truncated,
            };

            var resultElement = JsonSerializer.SerializeToElement(
                result, ProtocolJsonContext.Default.LogsGetEngineResult);

            return new UnaryHandlerResult(
                Response: new JsonRpcResponse { Result = resultElement },
                Continuation: Continuation.Continue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLogsGetEngineFailed(_logger, ex);
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = "Failed to read the engine log.",
                    },
                },
                Continuation: Continuation.Continue);
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

    private static async IAsyncEnumerable<JsonElement> MapFramesAsync(
        LogSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in subscription
            .ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return JsonSerializer.SerializeToElement(
                frame, ProtocolJsonContext.Default.LogStreamFrame);
        }
    }

    private UnaryHandlerResult HandleShutdown()
    {
        var result = new ShutdownResult { Accepted = true };
        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.ShutdownResult);

        return new UnaryHandlerResult(
            Response: new JsonRpcResponse { Result = resultElement },
            Continuation: Continuation.Complete,
            PostFlush: () =>
            {
                LogShutdownRequested(_logger);
                _lifetime.StopApplication();
                return Task.CompletedTask;
            });
    }

    [LoggerMessage(EventId = 50, Level = LogLevel.Debug,
        Message = "RPC dispatch saw unknown method '{Method}'.")]
    private static partial void LogMethodNotFound(ILogger logger, string method);

    [LoggerMessage(EventId = 51, Level = LogLevel.Warning,
        Message = "Engine.RegistryEntries handler failed to read the registry.")]
    private static partial void LogRegistryEntriesFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 52, Level = LogLevel.Information,
        Message = "Engine.Shutdown requested via RPC; initiating host stop.")]
    private static partial void LogShutdownRequested(ILogger logger);

    [LoggerMessage(EventId = 53, Level = LogLevel.Debug,
        Message = "RPC dispatch read faulted; closing connection.")]
    private static partial void LogReadFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 54, Level = LogLevel.Debug,
        Message = "RPC dispatch write faulted; closing connection.")]
    private static partial void LogWriteFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 55, Level = LogLevel.Debug,
        Message = "RPC frame failed to parse as JSON.")]
    private static partial void LogFrameParseFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 56, Level = LogLevel.Debug,
        Message = "RPC frame is not a valid JSON-RPC 2.0 request.")]
    private static partial void LogInvalidRequest(ILogger logger);

    [LoggerMessage(EventId = 57, Level = LogLevel.Debug,
        Message = "RPC dispatch could not parse params for '{Method}'.")]
    private static partial void LogParamsParseFailed(ILogger logger, string method, Exception exception);

    [LoggerMessage(EventId = 58, Level = LogLevel.Warning,
        Message = "Logs.GetEngine handler failed to read the engine log.")]
    private static partial void LogLogsGetEngineFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 59, Level = LogLevel.Debug,
        Message = "Logs.GetEngine rejected request with negative LastN={LastN}.")]
    private static partial void LogLogsGetEngineRejectedNegativeLastN(ILogger logger, int lastN);
}
