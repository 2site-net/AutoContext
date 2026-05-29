namespace AutoContext.Engine.Core.Rpc.Policies;

using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Outcome returned by a per-method handler invoked through
/// <see cref="IRpcConnectionPolicy.InvokeAsync"/>. Concrete
/// subtypes encode the two response shapes the processor knows
/// how to emit: a single <see cref="JsonRpcResponse"/> frame
/// (<see cref="UnaryHandlerResult"/>) or a sequence of
/// <see cref="JsonRpcStreamFrame"/> frames terminated by the
/// processor (<see cref="StreamingHandlerResult"/>).
/// </summary>
/// <param name="Continuation">What the processor should do after
/// flushing the response. See <see cref="Continuation"/>.</param>
/// <param name="PostFlush">Optional side effect to run after the
/// response has been written to the wire and before the
/// processor honours <paramref name="Continuation"/>. Used by
/// terminal methods that need their reply to land on the wire
/// before triggering a process-level state change — e.g.
/// <c>Engine.Shutdown</c>'s call to
/// <see cref="Microsoft.Extensions.Hosting.IHostApplicationLifetime.StopApplication"/>.</param>
internal abstract record RpcHandlerResult(
    Continuation Continuation,
    Func<Task>? PostFlush);
