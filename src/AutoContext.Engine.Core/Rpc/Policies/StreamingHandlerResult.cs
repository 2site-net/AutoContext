namespace AutoContext.Engine.Core.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Result that writes a server-streaming response — a sequence of
/// <see cref="JsonRpcStreamNext"/> frames synthesised from
/// <paramref name="Payloads"/>, terminated by exactly one
/// <see cref="JsonRpcStreamComplete"/> (on clean exhaust) or
/// <see cref="JsonRpcStreamError"/> (on iterator fault) frame
/// emitted by the processor itself.
/// </summary>
/// <remarks>
/// <para>
/// The handler yields only the per-frame payloads
/// (<see cref="JsonElement"/>); the processor owns the wire shape
/// (id correlation, kind discriminator, terminal frame). This
/// keeps streaming handlers ignorant of the envelope so a future
/// multiplex commit can extend the envelope additively without
/// touching every handler.
/// </para>
/// <para>
/// Streaming results are always terminal: the inherited
/// <see cref="RpcHandlerResult.Continuation"/> is fixed to
/// <see cref="Continuation.Complete"/> because the current
/// design specifies one stream per connection — the connection
/// closes once the stream ends.
/// </para>
/// </remarks>
/// <param name="Payloads">Per-frame payloads yielded by the
/// handler. The processor iterates this enumerable under the
/// connection-level cancellation token, wraps each element in a
/// <see cref="JsonRpcStreamNext"/> frame, and writes it to the
/// wire. If the iterator throws, the processor writes a
/// <see cref="JsonRpcStreamError"/> terminal frame; on clean
/// exhaust it writes a <see cref="JsonRpcStreamComplete"/>.</param>
/// <param name="PostFlush">Handler-supplied cleanup. Unlike the
/// unary path, the streaming processor invokes this in a
/// <c>finally</c> block, so it always runs — after a clean
/// <see cref="JsonRpcStreamComplete"/>, after a synthesised
/// <see cref="JsonRpcStreamError"/>, and also when the peer
/// closes mid-stream or connection-level cancellation fires.
/// This guarantees subscription disposal cannot leak when the
/// client hangs up before exhausting the stream.</param>
internal sealed record StreamingHandlerResult(
    IAsyncEnumerable<JsonElement> Payloads,
    Func<Task>? PostFlush = null)
    : RpcHandlerResult(Continuation.Complete, PostFlush);
