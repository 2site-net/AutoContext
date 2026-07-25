namespace AutoContext.Client.Core.Tests.Support;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

/// <summary>
/// A minimal stand-in for the engine's <c>rpc</c> pipe used by the
/// find-or-spawn tests. Binds one named-pipe server at a given endpoint
/// address, answers <c>Engine.Hello</c> with a configurable protocol
/// version, and echoes any other request's method name back as its
/// result. It exists so the resolver's dial + handshake + retry flow
/// can be exercised without standing up a whole engine.
/// </summary>
internal sealed class FakeEnginePipeServer : IAsyncDisposable
{
    private readonly Task _acceptLoop;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _pipeName;
    private readonly int _protocolVersion;
    private int _helloCount;

    public FakeEnginePipeServer(string pipeName, int protocolVersion)
    {
        _pipeName = pipeName;
        _protocolVersion = protocolVersion;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>Number of <c>Engine.Hello</c> frames received.</summary>
    public int HelloCount => Volatile.Read(ref _helloCount);

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        _cts.Dispose();
    }

    private NamedPipeServerStream CreateInstance()
        => new(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var instance = CreateInstance();
            await using (instance.ConfigureAwait(false))
            {
                try
                {
                    await instance.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    await HandleConnectionAsync(instance, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        var codec = new LengthPrefixedFrameCodec(server);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (bytes is null)
                {
                    return;
                }

                var request = JsonSerializer.Deserialize(
                    bytes, ProtocolJsonContext.Default.JsonRpcRequest);
                if (request is null)
                {
                    return;
                }

                var response = BuildResponse(request);
                var responseBytes = JsonSerializer.SerializeToUtf8Bytes(
                    response, ProtocolJsonContext.Default.JsonRpcResponse);
                await codec.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // Client disconnected or the server is shutting down.
        }
    }

    private JsonRpcResponse BuildResponse(JsonRpcRequest request)
    {
        if (request.Method == ProtocolMethods.Hello)
        {
            Interlocked.Increment(ref _helloCount);

            var handshake = JsonSerializer.SerializeToElement(
                new JsonHandshakeResult { ProtocolVersion = _protocolVersion, EngineVersion = "fake" },
                ProtocolJsonContext.Default.JsonHandshakeResult);
            return new JsonRpcResponse { Id = request.Id, Result = handshake };
        }

        var echo = JsonSerializer.SerializeToElement(request.Method);
        return new JsonRpcResponse { Id = request.Id, Result = echo };
    }
}
