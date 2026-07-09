namespace AutoContext.Engine.Core.Rpc.Results;

using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Result for a JSON-RPC 2.0 notification — an id-less request the
/// engine consumes without writing any response frame. The handler
/// has already applied the notification's side effect (e.g.
/// enqueuing a log record) by the time it returns this; the
/// processor writes nothing back and, unless
/// <paramref name="Continuation"/> says otherwise, keeps reading
/// the next frame.
/// </summary>
/// <remarks>
/// This is the third response shape the
/// <see cref="RpcConnectionProcessor"/> understands, alongside
/// <see cref="UnaryHandlerResult"/> (one
/// <see cref="JsonRpcResponse"/> frame) and
/// <see cref="StreamingHandlerResult"/> (a terminated stream of
/// <see cref="JsonRpcStreamFrame"/> frames). Fire-and-forget
/// notifications carry no <c>id</c>, so per JSON-RPC 2.0 the
/// server must not answer them — this result exists so a handler
/// can signal "consumed, reply nothing".
/// </remarks>
/// <param name="Continuation">What the processor should do after
/// the handler returns. Defaults to
/// <see cref="Continuation.Continue"/> — a notification is a
/// mid-stream event, not a connection terminator.</param>
/// <param name="PostFlush">Optional side effect to run after the
/// handler returns. Unlike the unary path there is no response to
/// flush first, so this runs immediately; a fault is logged and
/// swallowed so it cannot tear the accept loop down.</param>
internal sealed record NotificationHandlerResult(
    Continuation Continuation = Continuation.Continue,
    Func<Task>? PostFlush = null)
    : RpcHandlerResult(Continuation, PostFlush);
