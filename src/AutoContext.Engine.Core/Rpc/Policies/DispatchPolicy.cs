namespace AutoContext.Engine.Core.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IRpcConnectionPolicy"/> that runs after a successful
/// <c>Engine.Hello</c> handshake on an
/// <see cref="EndpointKind.Rpc"/> connection. Per
/// <c>design § RPC surface</c> the engine exposes two methods at
/// this stage of Phase 1 — <c>Engine.RegistryEntries</c> and
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
    private readonly ILogger _logger;

    public DispatchPolicy(
        IHostApplicationLifetime lifetime,
        RegistryFileReader registryReader,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(registryReader);
        ArgumentNullException.ThrowIfNull(logger);

        _lifetime = lifetime;
        _registryReader = registryReader;
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

            case ProtocolMethods.Shutdown:
                return HandleShutdown();

            default:
                LogMethodNotFound(_logger, request.Method);
                return new RpcHandlerResult(
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

            return new RpcHandlerResult(
                Response: new JsonRpcResponse { Result = resultElement },
                Continuation: Continuation.Continue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRegistryEntriesFailed(_logger, ex);
            return new RpcHandlerResult(
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

    private RpcHandlerResult HandleShutdown()
    {
        var result = new ShutdownResult { Accepted = true };
        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.ShutdownResult);

        return new RpcHandlerResult(
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
}
