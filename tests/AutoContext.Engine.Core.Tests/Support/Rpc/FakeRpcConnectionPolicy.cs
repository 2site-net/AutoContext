namespace AutoContext.Engine.Core.Tests.Support.Rpc;

using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Scriptable <see cref="IRpcConnectionPolicy"/> fake used to drive
/// the <see cref="RpcConnectionProcessor"/> against canned
/// outcomes. Tests configure <see cref="OnInvoke"/> to script the
/// per-request handler result, set <see cref="EndpointKind"/> and
/// <see cref="FrameFailurePolicy"/> to choose the policy variant,
/// and inspect the recorded counters to verify which framing-fault
/// hooks fired.
/// </summary>
internal sealed class FakeRpcConnectionPolicy : IRpcConnectionPolicy
{
    public EndpointKind EndpointKind { get; set; } = EndpointKind.Rpc;

    public FrameFailurePolicy FrameFailurePolicy { get; set; } = FrameFailurePolicy.Recover;

    public Func<JsonRpcRequest, CancellationToken, ValueTask<RpcHandlerResult>>? OnInvoke { get; set; }

    public int ReadFaultCount { get; private set; }

    public int WriteFaultCount { get; private set; }

    public int ParseFaultCount { get; private set; }

    public int InvalidRequestCount { get; private set; }

    public int ConnectionClosedByPeerCount { get; private set; }

    public Exception? LastReadFault { get; private set; }

    public Exception? LastWriteFault { get; private set; }

    public Exception? LastParseFault { get; private set; }

    public void LogFrameReadFault(Exception exception)
    {
        ReadFaultCount++;
        LastReadFault = exception;
    }

    public void LogFrameWriteFault(Exception exception)
    {
        WriteFaultCount++;
        LastWriteFault = exception;
    }

    public void LogFrameParseFault(Exception exception)
    {
        ParseFaultCount++;
        LastParseFault = exception;
    }

    public void LogFrameInvalidRequest() => InvalidRequestCount++;

    public void LogConnectionClosedByPeer() => ConnectionClosedByPeerCount++;

    public ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return OnInvoke is { } handler
            ? handler(request, cancellationToken)
            : throw new InvalidOperationException(
                $"{nameof(FakeRpcConnectionPolicy)}.{nameof(OnInvoke)} was not set.");
    }
}
