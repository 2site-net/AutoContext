namespace AutoContext.Engine.Core.Rpc;

using System.Text.Json;

using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Per-connection JSON-RPC request/response loop driven by an
/// <see cref="IRpcConnectionPolicy"/>. Reads one length-prefixed
/// JSON-RPC frame at a time, validates it, hands it to the policy
/// for routing, writes the response back, and consults the
/// policy-returned <see cref="Continuation"/> to decide whether to
/// keep serving, exit successfully, or exit with a failure.
/// </summary>
/// <remarks>
/// <para>
/// The processor is the single shared shell behind both
/// <c>Engine.Hello</c> handshake acceptance and post-handshake RPC
/// dispatch. The two consumers differ only in the policy they
/// supply — the handshake policy accepts exactly one method and
/// terminates the connection on any frame-level failure, while the
/// dispatch policy routes by method and recovers from per-frame
/// errors by writing the appropriate JSON-RPC error reply.
/// </para>
/// <para>
/// Per <c>design § Lifecycle &gt; Wire-protocol handshake</c>, the
/// reply to a request always carries the request's <c>id</c> echoed
/// verbatim; when the inbound request did not carry one, the
/// processor substitutes <see cref="JsonRpcId.Null"/>.
/// </para>
/// </remarks>
internal static partial class RpcConnectionProcessor
{
    /// <summary>
    /// Drives the request/response loop on
    /// <paramref name="stream"/> until the stream is closed by the
    /// peer, cancellation is requested, a handler returns a
    /// terminal <see cref="Continuation"/>, or the
    /// <see cref="IRpcConnectionPolicy.FrameFailurePolicy"/> calls
    /// for termination after a frame-level fault.
    /// </summary>
    /// <param name="stream">Connected pipe stream. Caller owns the
    /// lifetime; this method neither closes nor disposes it.</param>
    /// <param name="policy">Strategy that supplies log scope,
    /// frame-failure policy, and method routing.</param>
    /// <param name="logger">Logger for the processor's internal
    /// diagnostics (post-flush faults and defensive
    /// unknown-continuation reports). Framing-level diagnostics
    /// (read/write/parse/invalid-request, clean peer disconnect)
    /// are emitted by the policy via
    /// <see cref="IRpcConnectionPolicy.LogFrameReadFault"/> and
    /// friends.</param>
    /// <param name="cancellationToken">Cancellation token. Aborts
    /// the loop without writing any further reply.</param>
    /// <returns><see langword="true"/> when the loop exited via a
    /// handler returning <see cref="Continuation.Complete"/> (the
    /// connection completed successfully — handshake accepted or
    /// terminal RPC method acknowledged); otherwise
    /// <see langword="false"/> (EOF, cancellation, fault, abort,
    /// or terminate-on-frame-failure).</returns>
    public static async Task<bool> RunAsync(
        Stream stream,
        IRpcConnectionPolicy policy,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(policy);
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
                policy.LogFrameReadFault(ex);
                return false;
            }
            catch (ObjectDisposedException ex)
            {
                policy.LogFrameReadFault(ex);
                return false;
            }
            catch (InvalidDataException ex)
            {
                policy.LogFrameReadFault(ex);
                return false;
            }

            if (requestBytes is null)
            {
                // EOF — peer closed the connection. Not a fault;
                // not a success either (handler never returned the
                // terminal continuation).
                policy.LogConnectionClosedByPeer();
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
                policy.LogFrameParseFault(ex);
                var errorWritten = await TryWriteErrorAsync(
                    codec,
                    JsonRpcId.Null,
                    JsonRpcErrorCodes.ParseError,
                    "Frame is not valid JSON.",
                    policy,
                    cancellationToken).ConfigureAwait(false);

                if (!errorWritten ||
                    policy.FrameFailurePolicy == FrameFailurePolicy.Terminate)
                {
                    // Either the error reply failed to write (the
                    // stream is broken) or the policy requires the
                    // connection to die after any frame-level
                    // failure. Either way, no point reading
                    // another frame.
                    return false;
                }
                continue;
            }

            if (request is null || request.JsonRpc != JsonRpcVersion.Value)
            {
                var id = request is null
                    ? JsonRpcId.Null
                    : JsonRpcId.Normalize(request.Id);
                policy.LogFrameInvalidRequest();
                var errorWritten = await TryWriteErrorAsync(
                    codec,
                    id,
                    JsonRpcErrorCodes.InvalidRequest,
                    "Frame is not a valid JSON-RPC 2.0 request.",
                    policy,
                    cancellationToken).ConfigureAwait(false);

                if (!errorWritten ||
                    policy.FrameFailurePolicy == FrameFailurePolicy.Terminate)
                {
                    return false;
                }
                continue;
            }

            RpcHandlerResult handlerResult;
            try
            {
                handlerResult = await policy.InvokeAsync(request, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            bool? loopOutcome = handlerResult switch
            {
                UnaryHandlerResult unary => await WriteUnaryAsync(
                    codec, request, unary, policy, logger, cancellationToken)
                    .ConfigureAwait(false),
                StreamingHandlerResult streaming => await WriteStreamingAsync(
                    codec, request, streaming, policy, logger, cancellationToken)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException(
                    $"Unknown {nameof(RpcHandlerResult)} subtype "
                    + $"'{handlerResult.GetType().FullName}'."),
            };


            if (loopOutcome is null)
            {
                continue;
            }
            return loopOutcome.Value;
        }

        return false;
    }

    /// <summary>
    /// Writes a single <see cref="JsonRpcResponse"/> frame and
    /// applies the handler's <see cref="Continuation"/> + optional
    /// <see cref="UnaryHandlerResult.PostFlush"/>. Returns
    /// <see langword="null"/> when the loop should keep reading,
    /// or the success/failure bool for the outer
    /// <see cref="RunAsync"/> return value.
    /// </summary>
    private static async Task<bool?> WriteUnaryAsync(
        LengthPrefixedFrameCodec codec,
        JsonRpcRequest request,
        UnaryHandlerResult unary,
        IRpcConnectionPolicy policy,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Normalise the response id when the handler did not set
        // one — saves every handler from repeating the same
        // boilerplate.
        var response = unary.Response;
        if (response.Id.ValueKind == JsonValueKind.Undefined)
        {
            response = response with { Id = JsonRpcId.Normalize(request.Id) };
        }

        byte[] responseBytes;
        try
        {
            responseBytes = JsonSerializer.SerializeToUtf8Bytes(
                response, ProtocolJsonContext.Default.JsonRpcResponse);
        }
        catch (JsonException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }
        catch (NotSupportedException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }

        try
        {
            await codec.WriteAsync(responseBytes, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }

        if (unary.PostFlush is { } postFlush)
        {
            try
            {
                await postFlush().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation during PostFlush is treated as a
                // benign termination — the response is already on
                // the wire.
                return unary.Continuation == Continuation.Complete;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                // Intentional broad catch: the post-flush is an
                // arbitrary side effect supplied by the policy
                // (e.g. host shutdown). The response is already on
                // the wire — a fault here must not propagate out
                // of the connection processor and tear the accept
                // loop down.
                LogPostFlushFaulted(logger, ex, policy.EndpointKind);
                // The response flushed; the caller's intent
                // (Complete vs Abort) still stands.
            }
#pragma warning restore CA1031
        }

        return unary.Continuation switch
        {
            Continuation.Continue => null,
            Continuation.Complete => true,
            Continuation.Abort => false,
            _ => LogUnknownAndAbort(logger, policy.EndpointKind, unary.Continuation),
        };

        static bool LogUnknownAndAbort(ILogger logger, EndpointKind kind, Continuation value)
        {
            // Defensive: unknown enum value treated as a failure
            // to fail loud rather than silently dropping the
            // connection.
            LogUnknownContinuation(logger, kind, (int)value);
            return false;
        }
    }

    /// <summary>
    /// Drives a server-streaming response: iterates the handler's
    /// per-frame payloads under <paramref name="cancellationToken"/>,
    /// wraps each in a <see cref="JsonRpcStreamNext"/>, writes it
    /// to the wire, and synthesises the terminal
    /// <see cref="JsonRpcStreamComplete"/> (clean exhaust) or
    /// <see cref="JsonRpcStreamError"/> (iterator fault) frame.
    /// Returns <see langword="true"/> when the stream completed
    /// cleanly, or <see langword="false"/> when the peer closed
    /// mid-stream, cancellation fired, or the iterator faulted
    /// (no terminal frame is written for peer-close/cancellation
    /// because the wire is already torn down).
    /// </summary>
    /// <remarks>
    /// <see cref="StreamingHandlerResult.PostFlush"/> runs in a
    /// <c>finally</c> block: it always executes (even when the
    /// stream aborts), so handler-supplied cleanup such as
    /// subscription disposal cannot leak when the peer hangs up
    /// mid-stream.
    /// </remarks>
    private static async Task<bool> WriteStreamingAsync(
        LengthPrefixedFrameCodec codec,
        JsonRpcRequest request,
        StreamingHandlerResult streaming,
        IRpcConnectionPolicy policy,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var id = JsonRpcId.Normalize(request.Id);
        var iteratorFaulted = false;

        try
        {
            try
            {
                await foreach (var payload in streaming.Payloads
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false))
                {
                    var nextFrame = new JsonRpcStreamNext { Id = id, Result = payload };
                    if (!await TryWriteStreamFrameAsync(
                            codec, nextFrame, policy, cancellationToken).ConfigureAwait(false))
                    {
                        // Peer closed mid-stream or write faulted —
                        // no point trying to emit a terminal frame.
                        return false;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Connection-level cancellation: best effort exit
                // without writing a terminal frame.
                return false;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                // Intentional broad catch: the streaming payload
                // iterator is handler-supplied. Any fault is
                // captured and surfaced to the client as a
                // terminal error frame; it must not propagate out
                // of the processor. The exception is logged with
                // full detail; the client sees a generic message
                // to avoid leaking internal information.
                iteratorFaulted = true;
                LogStreamIteratorFaulted(logger, ex, policy.EndpointKind);
            }
#pragma warning restore CA1031

            var terminator = iteratorFaulted
                ? (JsonRpcStreamFrame)new JsonRpcStreamError
                {
                    Id = id,
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = "Streaming handler faulted.",
                    },
                }
                : new JsonRpcStreamComplete { Id = id };

            if (!await TryWriteStreamFrameAsync(
                    codec, terminator, policy, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            // A streamed response with a faulted iterator surfaced
            // an error frame on the wire — semantically a failed
            // RPC.
            return !iteratorFaulted;
        }
        finally
        {
            if (streaming.PostFlush is { } postFlush)
            {
                try
                {
                    await postFlush().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Benign — cleanup raced with cancellation.
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    // PostFlush is handler-supplied cleanup (e.g.
                    // subscription disposal). A fault here must
                    // not propagate out of the processor.
                    LogPostFlushFaulted(logger, ex, policy.EndpointKind);
                }
#pragma warning restore CA1031
            }
        }
    }

    private static async Task<bool> TryWriteStreamFrameAsync(
        LengthPrefixedFrameCodec codec,
        JsonRpcStreamFrame frame,
        IRpcConnectionPolicy policy,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(
                frame, ProtocolJsonContext.Default.JsonRpcStreamFrame);
        }
        catch (JsonException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }
        catch (NotSupportedException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }

        try
        {
            await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }
    }

    private static async Task<bool> TryWriteErrorAsync(
        LengthPrefixedFrameCodec codec,
        JsonElement id,
        int code,
        string message,
        IRpcConnectionPolicy policy,
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
            policy.LogFrameWriteFault(ex);
            return false;
        }
        catch (NotSupportedException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }

        try
        {
            await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            policy.LogFrameWriteFault(ex);
            return false;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "RPC connection post-flush side effect faulted on '{Kind}' endpoint; the response had already been written.")]
    private static partial void LogPostFlushFaulted(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "RPC connection on '{Kind}' endpoint observed unknown Continuation value {Value}; closing connection.")]
    private static partial void LogUnknownContinuation(ILogger logger, EndpointKind kind, int value);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "RPC streaming handler iterator faulted on '{Kind}' endpoint; surfacing a terminal error frame to the client.")]
    private static partial void LogStreamIteratorFaulted(ILogger logger, Exception exception, EndpointKind kind);
}
