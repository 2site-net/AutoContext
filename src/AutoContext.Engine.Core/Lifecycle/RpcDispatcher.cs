namespace AutoContext.Engine.Core.Lifecycle;

using System.Text.Json;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Per-connection JSON-RPC dispatch loop that runs after a
/// successful <c>Engine.Hello</c> handshake on an
/// <see cref="EndpointKind.Rpc"/> pipe. Reads one length-prefixed
/// JSON-RPC request at a time, routes it to the matching handler,
/// and writes the response back onto the same connection. Per
/// <c>design § RPC surface</c> the engine exposes two methods at
/// this stage of Phase 1 — <c>Engine.RegistryEntries</c> and
/// <c>Engine.Shutdown</c>; any other method name surfaces a
/// JSON-RPC <see cref="JsonRpcErrorCodes.MethodNotFound"/> reply
/// so callers can distinguish a typo from a packaging-version skew.
/// </summary>
/// <remarks>
/// <para>
/// The loop is intentionally narrow: it does not buffer pipelined
/// requests, it does not multiplex concurrent handlers on one
/// connection, and it does not interpret <c>Engine.Hello</c>
/// (the handshake step owns that). Recoverable per-frame failures
/// (malformed JSON, unknown method) reply with the appropriate
/// error code and continue serving subsequent frames; only stream
/// EOF, cancellation, or a successful <c>Engine.Shutdown</c> tears
/// the loop down.
/// </para>
/// <para>
/// <c>Engine.Shutdown</c> writes <c>{ accepted: true }</c> first,
/// then calls <see cref="IHostApplicationLifetime.StopApplication"/>
/// — exactly that order so the response is flushed onto the wire
/// before the host begins tearing down listeners. The hosted-service
/// stop sequence (which runs in reverse-registration order) then
/// drains and disposes the four pipes.
/// </para>
/// </remarks>
internal static partial class RpcDispatcher
{
    /// <summary>
    /// Drives the dispatch loop on <paramref name="stream"/> until
    /// the stream is closed by the peer, cancellation is requested,
    /// or a successful <c>Engine.Shutdown</c> response is flushed.
    /// </summary>
    /// <param name="stream">Connected pipe stream that has already
    /// completed the <c>Engine.Hello</c> handshake. Caller owns the
    /// lifetime; this method neither closes nor disposes it.</param>
    /// <param name="lifetime">Host lifetime used to request a
    /// graceful shutdown in response to <c>Engine.Shutdown</c>.</param>
    /// <param name="registryReader">Reader used to snapshot the
    /// machine-wide engine-liveness registry for
    /// <c>Engine.RegistryEntries</c>.</param>
    /// <param name="logger">Logger for per-frame diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token. Aborts
    /// the loop without writing any further reply.</param>
    public static async Task DispatchAsync(
        Stream stream,
        IHostApplicationLifetime lifetime,
        RegistryFileReader registryReader,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(registryReader);
        ArgumentNullException.ThrowIfNull(logger);

        var codec = new LengthPrefixedFrameCodec(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            byte[]? requestBytes;
            try
            {
                requestBytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                LogReadFaulted(logger, ex);
                return;
            }
            catch (ObjectDisposedException ex)
            {
                LogReadFaulted(logger, ex);
                return;
            }
            catch (InvalidDataException ex)
            {
                LogReadFaulted(logger, ex);
                return;
            }

            if (requestBytes is null)
            {
                // EOF — peer closed the connection.
                return;
            }

            JsonRpcRequest? request;
            try
            {
                request = JsonSerializer.Deserialize(
                    requestBytes, ProtocolJsonContext.Default.JsonRpcRequest);
            }
            catch (JsonException ex)
            {
                LogFrameParseFailed(logger, ex);
                if (!await TryWriteErrorAsync(
                        codec,
                        NullId,
                        JsonRpcErrorCodes.ParseError,
                        "Frame is not valid JSON.",
                        logger,
                        cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
                continue;
            }

            if (request is null || request.Jsonrpc != JsonRpcVersion.Value)
            {
                var id = request is null ? NullId : NormalizeId(request.Id);
                LogInvalidRequest(logger);
                if (!await TryWriteErrorAsync(
                        codec,
                        id,
                        JsonRpcErrorCodes.InvalidRequest,
                        "Frame is not a valid JSON-RPC 2.0 request.",
                        logger,
                        cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
                continue;
            }

            var responseId = NormalizeId(request.Id);
            var shouldStop = false;
            JsonRpcResponse response;

            switch (request.Method)
            {
                case RegistryMethods.RegistryEntries:
                    response = await HandleRegistryEntriesAsync(
                        responseId, registryReader, logger, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case ProtocolMethods.Shutdown:
                    response = HandleShutdown(responseId);
                    shouldStop = true;
                    break;

                default:
                    LogMethodNotFound(logger, request.Method);
                    response = new JsonRpcResponse
                    {
                        Id = responseId,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.MethodNotFound,
                            Message = $"Unknown method '{request.Method}'.",
                        },
                    };
                    break;
            }

            byte[] responseBytes;
            try
            {
                responseBytes = JsonSerializer.SerializeToUtf8Bytes(
                    response, ProtocolJsonContext.Default.JsonRpcResponse);
            }
            catch (JsonException ex)
            {
                LogWriteFaulted(logger, ex);
                return;
            }
            catch (NotSupportedException ex)
            {
                LogWriteFaulted(logger, ex);
                return;
            }

            try
            {
                await codec.WriteAsync(responseBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                LogWriteFaulted(logger, ex);
                return;
            }
            catch (ObjectDisposedException ex)
            {
                LogWriteFaulted(logger, ex);
                return;
            }

            if (shouldStop)
            {
                // Response is flushed; ask the host to begin its
                // ordered stop sequence. The stop will cancel this
                // method's token and the accept loops, draining the
                // four pipes in reverse-registration order.
                LogShutdownRequested(logger);
                lifetime.StopApplication();
                return;
            }
        }
    }

    private static readonly JsonElement NullId =
        JsonDocument.Parse("null").RootElement;

    private static JsonElement NormalizeId(JsonElement id) =>
        id.ValueKind == JsonValueKind.Undefined ? NullId : id;

    private static async Task<JsonRpcResponse> HandleRegistryEntriesAsync(
        JsonElement responseId,
        RegistryFileReader registryReader,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await registryReader.ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = new RegistryEntriesResult
            {
                Entries = entries,
            };

            var resultElement = JsonSerializer.SerializeToElement(
                result, ProtocolJsonContext.Default.RegistryEntriesResult);

            return new JsonRpcResponse
            {
                Id = responseId,
                Result = resultElement,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRegistryEntriesFailed(logger, ex);
            return new JsonRpcResponse
            {
                Id = responseId,
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InternalError,
                    Message = "Failed to read the engine registry.",
                },
            };
        }
    }

    private static JsonRpcResponse HandleShutdown(JsonElement responseId)
    {
        var result = new ShutdownResult { Accepted = true };
        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.ShutdownResult);

        return new JsonRpcResponse
        {
            Id = responseId,
            Result = resultElement,
        };
    }

    private static async Task<bool> TryWriteErrorAsync(
        LengthPrefixedFrameCodec codec,
        JsonElement id,
        int code,
        string message,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var response = new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message,
            },
        };

        byte[] bytes;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(
                response, ProtocolJsonContext.Default.JsonRpcResponse);
        }
        catch (JsonException ex)
        {
            LogWriteFaulted(logger, ex);
            return false;
        }
        catch (NotSupportedException ex)
        {
            LogWriteFaulted(logger, ex);
            return false;
        }

        try
        {
            await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException ex)
        {
            LogWriteFaulted(logger, ex);
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            LogWriteFaulted(logger, ex);
            return false;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "RPC dispatch read faulted; closing connection.")]
    private static partial void LogReadFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "RPC dispatch write faulted; closing connection.")]
    private static partial void LogWriteFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "RPC frame failed to parse as JSON.")]
    private static partial void LogFrameParseFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "RPC frame is not a valid JSON-RPC 2.0 request.")]
    private static partial void LogInvalidRequest(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "RPC dispatch saw unknown method '{Method}'.")]
    private static partial void LogMethodNotFound(ILogger logger, string method);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning,
        Message = "Engine.RegistryEntries handler failed to read the registry.")]
    private static partial void LogRegistryEntriesFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "Engine.Shutdown requested via RPC; initiating host stop.")]
    private static partial void LogShutdownRequested(ILogger logger);
}
