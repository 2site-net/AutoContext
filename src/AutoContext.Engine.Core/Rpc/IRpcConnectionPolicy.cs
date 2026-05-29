namespace AutoContext.Engine.Core.Rpc;

using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Strategy contract for the <see cref="RpcConnectionProcessor"/>.
/// A policy captures everything that varies between an
/// <c>Engine.Hello</c> handshake connection and a post-handshake
/// JSON-RPC dispatch connection — log scope, frame-failure
/// behaviour, and the per-method handler table — while leaving the
/// shared framing concerns (read length-prefixed frame, parse
/// JSON-RPC 2.0, write response, exception mapping) to the
/// processor itself.
/// </summary>
internal interface IRpcConnectionPolicy
{
    /// <summary>
    /// Endpoint kind the connection is bound to. Threaded through
    /// the processor's internal log messages (post-flush faults
    /// and unknown-continuation reports) and available to the
    /// policy's own log messages so failures attribute to
    /// <c>rpc</c> vs <c>events</c>.
    /// </summary>
    EndpointKind EndpointKind { get; }

    /// <summary>
    /// What the processor should do when a frame fails to parse as
    /// JSON or fails JSON-RPC 2.0 validation. See
    /// <see cref="FrameFailurePolicy"/> for the modes.
    /// </summary>
    FrameFailurePolicy FrameFailurePolicy { get; }

    /// <summary>
    /// Logs a stream-level read fault (typically an
    /// <see cref="IOException"/> or
    /// <see cref="ObjectDisposedException"/>). Each policy picks
    /// its own severity — the handshake treats the fault as a
    /// loud surface for packaging bugs (Warning); the post-
    /// handshake dispatch loop treats it as recoverable noise
    /// (Debug).
    /// </summary>
    void LogFrameReadFault(Exception exception);

    /// <summary>
    /// Logs a stream-level write fault.
    /// See <see cref="LogFrameReadFault"/> for severity rationale.
    /// </summary>
    void LogFrameWriteFault(Exception exception);

    /// <summary>
    /// Logs a frame that failed to parse as JSON. The processor
    /// has already written (or attempted to write) a
    /// <see cref="JsonRpcErrorCodes.ParseError"/> reply.
    /// See <see cref="LogFrameReadFault"/> for severity rationale.
    /// </summary>
    void LogFrameParseFault(Exception exception);

    /// <summary>
    /// Logs a frame that parsed as JSON but is not a valid
    /// JSON-RPC 2.0 request. The processor has already written
    /// (or attempted to write) a
    /// <see cref="JsonRpcErrorCodes.InvalidRequest"/> reply.
    /// See <see cref="LogFrameReadFault"/> for severity rationale.
    /// </summary>
    void LogFrameInvalidRequest();

    /// <summary>
    /// Logs that the peer closed the connection cleanly (read
    /// returned EOF) before the handler returned a terminal
    /// <see cref="Continuation"/>.
    /// </summary>
    void LogConnectionClosedByPeer();

    /// <summary>
    /// Dispatches <paramref name="request"/> to the matching
    /// handler and returns the response, the continuation, and any
    /// post-flush side effect. Implementations decide how to
    /// surface an unknown method: the handshake policy aborts the
    /// connection with
    /// <see cref="JsonRpcErrorCodes.HelloRequired"/>; the dispatch
    /// policy returns
    /// <see cref="JsonRpcErrorCodes.MethodNotFound"/> and keeps
    /// serving.
    /// </summary>
    /// <param name="request">Parsed inbound request. The processor
    /// guarantees this is a well-formed JSON-RPC 2.0 request whose
    /// <c>jsonrpc</c> field equals <c>"2.0"</c>.</param>
    /// <param name="cancellationToken">Cancellation token observed
    /// by the handler. Honouring it is recommended but not
    /// mandatory; the processor will short-circuit the loop on the
    /// next iteration regardless.</param>
    ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken);
}
