namespace AutoContext.Engine.Core.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IRpcConnectionPolicy"/> that enforces the mandatory
/// <c>Engine.Hello</c> handshake at the head of every
/// <see cref="EndpointKind.Rpc"/> and
/// <see cref="EndpointKind.Events"/> connection per
/// <c>design § Lifecycle &gt; Wire-protocol handshake</c>.
/// </summary>
/// <remarks>
/// <para>
/// The policy is deliberately narrow: it accepts exactly one
/// method (<see cref="ProtocolMethods.Hello"/>) and treats any
/// frame-level failure as a connection-fatal event
/// (<see cref="FrameFailurePolicy.Terminate"/>). On a successful
/// match it validates the request params and either returns a
/// <see cref="JsonHandshakeResult"/> with
/// <see cref="Continuation.Complete"/> (handshake accepted; caller
/// proceeds to the dispatch or events-pump phase) or a structured
/// JSON-RPC error reply with <see cref="Continuation.Abort"/>
/// (caller closes the stream).
/// </para>
/// <para>
/// Exact-match version semantics are deliberate. Each host bundles
/// its own engine binary, so a mismatch is a packaging bug, not a
/// scenario the protocol tries to recover from; refusing hard
/// surfaces the bug instead of silently downgrading behaviour.
/// </para>
/// </remarks>
internal sealed partial class HandshakePolicy : IRpcConnectionPolicy
{
    private readonly ILogger _logger;

    public HandshakePolicy(EndpointKind kind, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        EndpointKind = kind;
        _logger = logger;
    }

    public EndpointKind EndpointKind { get; }

    public FrameFailurePolicy FrameFailurePolicy => FrameFailurePolicy.Terminate;

    public void LogFrameReadFault(Exception exception) =>
        LogHandshakeReadFaulted(_logger, exception, EndpointKind);

    public void LogFrameWriteFault(Exception exception) =>
        LogHandshakeWriteFaulted(_logger, exception, EndpointKind);

    public void LogFrameParseFault(Exception exception) =>
        LogHandshakeParseFailed(_logger, exception, EndpointKind);

    public void LogFrameInvalidRequest() =>
        LogHandshakeInvalidRequest(_logger, EndpointKind);

    public void LogConnectionClosedByPeer() =>
        LogHandshakeAbandoned(_logger, EndpointKind);

    public ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Method != ProtocolMethods.Hello)
        {
            LogHandshakeWrongMethod(_logger, EndpointKind, request.Method);
            return new ValueTask<RpcHandlerResult>(BuildAbort(
                JsonRpcErrorCodes.HelloRequired,
                $"First frame on '{EndpointKind}' endpoint must invoke '{ProtocolMethods.Hello}'."));
        }

        JsonHandshakeParams? helloParams;
        try
        {
            helloParams = request.Params is { } paramsElement
                && paramsElement.ValueKind != JsonValueKind.Undefined
                    ? paramsElement.Deserialize(ProtocolJsonContext.Default.JsonHandshakeParams)
                    : null;
        }
        catch (JsonException ex)
        {
            LogHandshakeInvalidParams(_logger, ex, EndpointKind);
            return new ValueTask<RpcHandlerResult>(BuildAbort(
                JsonRpcErrorCodes.InvalidParams,
                $"'{ProtocolMethods.Hello}' params are not deserialisable."));
        }

        if (helloParams is null)
        {
            LogHandshakeInvalidParams(_logger, null, EndpointKind);
            return new ValueTask<RpcHandlerResult>(BuildAbort(
                JsonRpcErrorCodes.InvalidParams,
                $"'{ProtocolMethods.Hello}' requires a params object."));
        }

        if (helloParams.ProtocolVersion is not int clientProtocolVersion)
        {
            LogHandshakeInvalidParams(_logger, null, EndpointKind);
            return new ValueTask<RpcHandlerResult>(BuildAbort(
                JsonRpcErrorCodes.InvalidParams,
                $"'{ProtocolMethods.Hello}' params must include 'protocolVersion'."));
        }

        if (clientProtocolVersion != ProtocolVersion.Current)
        {
            LogHandshakeVersionMismatch(_logger, EndpointKind, clientProtocolVersion, ProtocolVersion.Current);
            return new ValueTask<RpcHandlerResult>(BuildAbort(
                JsonRpcErrorCodes.ProtocolVersionMismatch,
                $"Protocol version mismatch: engine speaks {ProtocolVersion.Current}, client sent {clientProtocolVersion}."));
        }

        var result = new JsonHandshakeResult
        {
            ProtocolVersion = ProtocolVersion.Current,
            EngineVersion = EngineVersion.Value,
        };

        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.JsonHandshakeResult);

        LogHandshakeAccepted(_logger, EndpointKind, clientProtocolVersion);

        return new ValueTask<RpcHandlerResult>(new UnaryHandlerResult(
            Response: new JsonRpcResponse { Result = resultElement },
            Continuation: Continuation.Complete));
    }

    private static UnaryHandlerResult BuildAbort(int code, string message) =>
        new(
            Response: new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = message,
                },
            },
            Continuation: Continuation.Abort);

    [LoggerMessage(EventId = 100, Level = LogLevel.Information,
        Message = "Engine.Hello handshake accepted on '{Kind}' endpoint (client protocol version {ClientProtocolVersion}).")]
    private static partial void LogHandshakeAccepted(ILogger logger, EndpointKind kind, int clientProtocolVersion);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: first frame on '{Kind}' endpoint invoked '{Method}' instead of 'Engine.Hello'.")]
    private static partial void LogHandshakeWrongMethod(ILogger logger, EndpointKind kind, string method);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: params on '{Kind}' endpoint are missing or malformed.")]
    private static partial void LogHandshakeInvalidParams(ILogger logger, Exception? exception, EndpointKind kind);

    [LoggerMessage(EventId = 103, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: client on '{Kind}' endpoint speaks protocol version {ClientProtocolVersion}; engine speaks {EngineProtocolVersion}.")]
    private static partial void LogHandshakeVersionMismatch(
        ILogger logger, EndpointKind kind, int clientProtocolVersion, int engineProtocolVersion);

    [LoggerMessage(EventId = 104, Level = LogLevel.Debug,
        Message = "Engine.Hello handshake abandoned: client on '{Kind}' endpoint closed the connection before sending any frame.")]
    private static partial void LogHandshakeAbandoned(ILogger logger, EndpointKind kind);

    [LoggerMessage(EventId = 105, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake read failed on '{Kind}' endpoint; connection will be closed.")]
    private static partial void LogHandshakeReadFaulted(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 106, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake write failed on '{Kind}' endpoint; connection will be closed.")]
    private static partial void LogHandshakeWriteFaulted(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 107, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: first frame on '{Kind}' endpoint was not valid JSON.")]
    private static partial void LogHandshakeParseFailed(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 108, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: first frame on '{Kind}' endpoint is not a valid JSON-RPC 2.0 request.")]
    private static partial void LogHandshakeInvalidRequest(ILogger logger, EndpointKind kind);
}
