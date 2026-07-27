namespace AutoContext.Client.Core.Tests.Support.Engine.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

/// <summary>
/// A minimal stand-in for the engine's <c>rpc</c> pipe used by the
/// find-or-spawn tests. Binds one endpoint through
/// <see cref="EnginePipeServerHarness"/> (so its bind and accept
/// semantics match the engine's), answers <c>Engine.Hello</c> with a
/// configurable protocol version, and echoes any other request's method
/// name back as its result. It exists so the resolver's dial + handshake
/// + retry flow can be exercised without standing up a whole engine.
/// </summary>
internal sealed class FakeEnginePipeServer : IAsyncDisposable
{
    private readonly EnginePipeServerHarness _harness;
    private readonly int _protocolVersion;
    private int _helloCount;

    public FakeEnginePipeServer(string pipeName, int protocolVersion)
    {
        _protocolVersion = protocolVersion;
        _harness = new EnginePipeServerHarness(pipeName, HandleConnectionAsync);
    }

    /// <summary>Number of <c>Engine.Hello</c> frames received.</summary>
    public int HelloCount => Volatile.Read(ref _helloCount);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => _harness.DisposeAsync();

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

    private async Task HandleConnectionAsync(Stream connection, CancellationToken cancellationToken)
    {
        var codec = new LengthPrefixedFrameCodec(connection);
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
}
