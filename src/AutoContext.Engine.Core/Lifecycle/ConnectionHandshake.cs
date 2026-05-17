namespace AutoContext.Engine.Core.Lifecycle;

using System.Reflection;
using System.Text.Json;

using AutoContext.Framework.Pipes;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// Performs the mandatory <c>Engine.Hello</c> handshake at the head
/// of every <see cref="EndpointKind.Rpc"/> and
/// <see cref="EndpointKind.Events"/> connection
/// per <c>design § Lifecycle &gt; Wire-protocol handshake</c>.
/// </summary>
/// <remarks>
/// <para>
/// The handshake is intentionally narrow: read one length-prefixed
/// JSON-RPC frame; if it is a well-formed <c>Engine.Hello</c> whose
/// <c>protocolVersion</c> exactly matches
/// <see cref="ProtocolVersion.Current"/>, reply with the engine's
/// <see cref="HandshakeResult"/> and report success. Any other
/// outcome — parse failure, wrong method, wrong version, malformed
/// params — writes a structured JSON-RPC error reply (when possible)
/// and reports failure, leaving the caller to close the stream.
/// </para>
/// <para>
/// Exact-match version semantics are deliberate. Each host bundles
/// its own engine binary, so a mismatch is a packaging bug, not a
/// scenario the protocol tries to recover from; refusing hard
/// surfaces the bug instead of silently downgrading behaviour.
/// </para>
/// </remarks>
internal static partial class ConnectionHandshake
{
    private static readonly string EngineSemver =
        typeof(ConnectionHandshake).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? typeof(ConnectionHandshake).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>
    /// Lazy-built JSON <c>null</c> element used as the response
    /// <c>id</c> when the inbound request omitted it. JSON-RPC 2.0
    /// requires the response to carry <c>"id": null</c> in that case
    /// rather than skipping the field.
    /// </summary>
    private static readonly JsonElement NullIdElement =
        JsonDocument.Parse("null").RootElement;

    /// <summary>
    /// Drives the handshake against <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Connected pipe stream. Caller owns the
    /// lifetime; this method neither closes nor disposes it.</param>
    /// <param name="kind">Endpoint kind the connection is bound to —
    /// included on diagnostic messages so failures attribute to
    /// <c>rpc</c> vs <c>events</c>.</param>
    /// <param name="logger">Logger for handshake diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token. Aborts
    /// the handshake without writing any reply.</param>
    /// <returns><see langword="true"/> when the handshake completed
    /// successfully and the connection may proceed; otherwise
    /// <see langword="false"/> — caller closes the stream.</returns>
    public static async Task<bool> TryAcceptAsync(
        Stream stream,
        EndpointKind kind,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(logger);

        var codec = new LengthPrefixedFrameCodec(stream);

        byte[]? requestBytes;
        try
        {
            requestBytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            LogHandshakeReadFaulted(logger, ex, kind);
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            LogHandshakeReadFaulted(logger, ex, kind);
            return false;
        }
        catch (InvalidDataException ex)
        {
            LogHandshakeReadFaulted(logger, ex, kind);
            return false;
        }

        if (requestBytes is null)
        {
            LogHandshakeAbandoned(logger, kind);
            return false;
        }

        JsonRpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(
                requestBytes, ProtocolJsonContext.Default.JsonRpcRequest);
        }
        catch (JsonException ex)
        {
            await TryWriteErrorAsync(
                codec,
                NullIdElement,
                JsonRpcErrorCodes.ParseError,
                "Frame is not valid JSON.",
                logger,
                kind,
                cancellationToken).ConfigureAwait(false);
            LogHandshakeParseFailed(logger, ex, kind);
            return false;
        }

        if (request is null || request.Jsonrpc != JsonRpcVersion.Value)
        {
            var id = request is null ? NullIdElement : NormalizeId(request.Id);
            await TryWriteErrorAsync(
                codec,
                id,
                JsonRpcErrorCodes.InvalidRequest,
                "Frame is not a valid JSON-RPC 2.0 request.",
                logger,
                kind,
                cancellationToken).ConfigureAwait(false);
            LogHandshakeInvalidRequest(logger, kind);
            return false;
        }

        if (request.Method != ProtocolMethods.Hello)
        {
            await TryWriteErrorAsync(
                codec,
                NormalizeId(request.Id),
                JsonRpcErrorCodes.HelloRequired,
                $"First frame on '{kind}' endpoint must invoke '{ProtocolMethods.Hello}'.",
                logger,
                kind,
                cancellationToken).ConfigureAwait(false);
            LogHandshakeWrongMethod(logger, kind, request.Method);
            return false;
        }

        HandshakeParams? helloParams;
        try
        {
            helloParams = request.Params is { } paramsElement
                && paramsElement.ValueKind != JsonValueKind.Undefined
                    ? paramsElement.Deserialize(ProtocolJsonContext.Default.HandshakeParams)
                    : null;
        }
        catch (JsonException ex)
        {
            await TryWriteErrorAsync(
                codec,
                NormalizeId(request.Id),
                JsonRpcErrorCodes.InvalidParams,
                $"'{ProtocolMethods.Hello}' params are not deserialisable.",
                logger,
                kind,
                cancellationToken).ConfigureAwait(false);
            LogHandshakeInvalidParams(logger, ex, kind);
            return false;
        }

        if (helloParams is null)
        {
            await TryWriteErrorAsync(
                codec,
                NormalizeId(request.Id),
                JsonRpcErrorCodes.InvalidParams,
                $"'{ProtocolMethods.Hello}' requires a params object.",
                logger,
                kind,
                cancellationToken).ConfigureAwait(false);
            LogHandshakeInvalidParams(logger, null, kind);
            return false;
        }

        if (helloParams.ProtocolVersion is not int clientProtocolVersion)
        {
            await TryWriteErrorAsync(
                codec,
                NormalizeId(request.Id),
                JsonRpcErrorCodes.InvalidParams,
                $"'{ProtocolMethods.Hello}' params must include 'protocolVersion'.",
                logger,
                kind,
                cancellationToken).ConfigureAwait(false);
            LogHandshakeInvalidParams(logger, null, kind);
            return false;
        }

        if (clientProtocolVersion != ProtocolVersion.Current)
        {
            await TryWriteErrorAsync(
                codec,
                NormalizeId(request.Id),
                JsonRpcErrorCodes.ProtocolVersionMismatch,
                $"Protocol version mismatch: engine speaks {ProtocolVersion.Current}, client sent {clientProtocolVersion}.",
                logger,
                kind,
                cancellationToken).ConfigureAwait(false);
            LogHandshakeVersionMismatch(logger, kind, clientProtocolVersion, ProtocolVersion.Current);
            return false;
        }

        var result = new HandshakeResult
        {
            ProtocolVersion = ProtocolVersion.Current,
            EngineVersion = EngineSemver,
        };

        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.HandshakeResult);

        var response = new JsonRpcResponse
        {
            Id = NormalizeId(request.Id),
            Result = resultElement,
        };

        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(
            response, ProtocolJsonContext.Default.JsonRpcResponse);

        try
        {
            await codec.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            LogHandshakeWriteFaulted(logger, ex, kind);
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            LogHandshakeWriteFaulted(logger, ex, kind);
            return false;
        }

        LogHandshakeAccepted(logger, kind, clientProtocolVersion);
        return true;
    }

    private static JsonElement NormalizeId(JsonElement id) =>
        id.ValueKind == JsonValueKind.Undefined ? NullIdElement : id;

    private static async Task TryWriteErrorAsync(
        LengthPrefixedFrameCodec codec,
        JsonElement id,
        int code,
        string message,
        ILogger logger,
        EndpointKind kind,
        CancellationToken cancellationToken)
    {
        var error = new JsonRpcError
        {
            Code = code,
            Message = message,
        };

        var response = new JsonRpcResponse
        {
            Id = id,
            Error = error,
        };

        byte[] bytes;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(
                response, ProtocolJsonContext.Default.JsonRpcResponse);
        }
        catch (JsonException ex)
        {
            LogHandshakeWriteFaulted(logger, ex, kind);
            return;
        }
        catch (NotSupportedException ex)
        {
            LogHandshakeWriteFaulted(logger, ex, kind);
            return;
        }

        try
        {
            await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            LogHandshakeWriteFaulted(logger, ex, kind);
        }
        catch (ObjectDisposedException ex)
        {
            LogHandshakeWriteFaulted(logger, ex, kind);
        }
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Information,
        Message = "Engine.Hello handshake accepted on '{Kind}' endpoint (client protocol version {ClientProtocolVersion}).")]
    private static partial void LogHandshakeAccepted(ILogger logger, EndpointKind kind, int clientProtocolVersion);

    [LoggerMessage(EventId = 101, Level = LogLevel.Debug,
        Message = "Engine.Hello handshake abandoned: client on '{Kind}' endpoint closed the connection before sending any frame.")]
    private static partial void LogHandshakeAbandoned(ILogger logger, EndpointKind kind);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake read failed on '{Kind}' endpoint; connection will be closed.")]
    private static partial void LogHandshakeReadFaulted(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 103, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake write failed on '{Kind}' endpoint; connection will be closed.")]
    private static partial void LogHandshakeWriteFaulted(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 104, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: first frame on '{Kind}' endpoint was not valid JSON.")]
    private static partial void LogHandshakeParseFailed(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 105, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: first frame on '{Kind}' endpoint is not a valid JSON-RPC 2.0 request.")]
    private static partial void LogHandshakeInvalidRequest(ILogger logger, EndpointKind kind);

    [LoggerMessage(EventId = 106, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: first frame on '{Kind}' endpoint invoked '{Method}' instead of 'Engine.Hello'.")]
    private static partial void LogHandshakeWrongMethod(ILogger logger, EndpointKind kind, string method);

    [LoggerMessage(EventId = 107, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: params on '{Kind}' endpoint are missing or malformed.")]
    private static partial void LogHandshakeInvalidParams(ILogger logger, Exception? exception, EndpointKind kind);

    [LoggerMessage(EventId = 108, Level = LogLevel.Warning,
        Message = "Engine.Hello handshake refused: client on '{Kind}' endpoint speaks protocol version {ClientProtocolVersion}; engine speaks {EngineProtocolVersion}.")]
    private static partial void LogHandshakeVersionMismatch(
        ILogger logger, EndpointKind kind, int clientProtocolVersion, int engineProtocolVersion);
}
