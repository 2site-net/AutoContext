namespace AutoContext.Engine.Core.Rpc.Policies;

using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Outcome returned by a per-method handler invoked through
/// <see cref="IRpcConnectionPolicy.InvokeAsync"/>. Encodes the
/// response frame to send, what the
/// <see cref="RpcConnectionProcessor"/> should do after flushing
/// it, and an optional side effect to run after the flush
/// completes successfully.
/// </summary>
/// <param name="Response">The JSON-RPC 2.0 response frame to write.
/// The handler is responsible for setting either <c>Result</c> or
/// <c>Error</c> (never both) and for echoing the request id —
/// when the handler leaves <c>Id</c> at
/// <see cref="System.Text.Json.JsonValueKind.Undefined"/> the
/// processor normalises it from the original request id.</param>
/// <param name="Continuation">What the processor should do after
/// flushing <paramref name="Response"/>. See
/// <see cref="Continuation"/> for the three modes.</param>
/// <param name="PostFlush">Optional side effect to run after the
/// response has been written to the wire and before the
/// processor honours <paramref name="Continuation"/>. Used by
/// terminal methods that need their reply to land on the wire
/// before triggering a process-level state change — e.g.
/// <c>Engine.Shutdown</c>'s call to
/// <see cref="Microsoft.Extensions.Hosting.IHostApplicationLifetime.StopApplication"/>.</param>
internal sealed record RpcHandlerResult(
    JsonRpcResponse Response,
    Continuation Continuation = Continuation.Continue,
    Func<Task>? PostFlush = null);
