namespace AutoContext.Client.Core.Tests.Support.Engine.Rpc;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

/// <summary>
/// A scripted stand-in for the engine's <c>rpc</c> or <c>events</c>
/// pipe used by the subscription tests. Binds one endpoint through
/// <see cref="EnginePipeServerHarness"/>, answers the mandatory
/// <c>Engine.Hello</c> handshake, then hands the connection to a
/// caller-supplied script that reads the subscribe request (rpc) or
/// immediately pushes frames (events). Unlike
/// <see cref="FakeEnginePipeServer"/> it can drive a full
/// server-streaming or notification sequence, so the subscription
/// consumers can be exercised through the real find-or-spawn resolver.
/// </summary>
internal sealed class ScriptedEnginePipeServer : IAsyncDisposable
{
    private readonly EnginePipeServerHarness _harness;
    private readonly Func<ScriptedPeer, CancellationToken, Task> _onConnected;
    private readonly int _protocolVersion;

    public ScriptedEnginePipeServer(
        string pipeName, int protocolVersion, Func<ScriptedPeer, CancellationToken, Task> onConnected)
    {
        _protocolVersion = protocolVersion;
        _onConnected = onConnected;
        _harness = new EnginePipeServerHarness(pipeName, HandleConnectionAsync);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => _harness.DisposeAsync();

    private async Task HandleConnectionAsync(Stream connection, CancellationToken cancellationToken)
    {
        var peer = new ScriptedPeer(new LengthPrefixedFrameCodec(connection));

        var hello = await peer.ReadRequestAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(hello.Method, ProtocolMethods.Hello, StringComparison.Ordinal))
        {
            var handshake = JsonSerializer.SerializeToElement(
                new JsonHandshakeResult { ProtocolVersion = _protocolVersion, EngineVersion = "scripted" },
                ProtocolJsonContext.Default.JsonHandshakeResult);
            await peer.WriteResponseAsync(hello.Id, handshake, cancellationToken).ConfigureAwait(false);
        }

        await _onConnected(peer, cancellationToken).ConfigureAwait(false);

        // Hold the connection open until the server is disposed so the
        // client reliably reads every scripted frame before teardown.
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// The server side of one scripted connection: reads the subscribe
/// request and writes handshake, streaming, and notification frames the
/// subscription consumers decode.
/// </summary>
internal sealed class ScriptedPeer(LengthPrefixedFrameCodec codec)
{
    public async Task<JsonRpcRequest> ReadRequestAsync(CancellationToken cancellationToken)
    {
        var bytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The client closed the connection before sending a frame.");

        return JsonSerializer.Deserialize(bytes, ProtocolJsonContext.Default.JsonRpcRequest)
            ?? throw new InvalidOperationException("The client sent an empty frame.");
    }

    public Task WriteNotificationAsync(
        string method, JsonElement parameters, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcNotification { Method = method, Params = parameters },
            ProtocolJsonContext.Default.JsonRpcNotification,
            cancellationToken);

    public Task WriteResponseAsync(JsonElement id, JsonElement result, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcResponse { Id = id, Result = result },
            ProtocolJsonContext.Default.JsonRpcResponse,
            cancellationToken);

    public Task WriteStreamNextAsync(JsonElement id, JsonElement result, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcStreamNext { Id = id, Result = result },
            ProtocolJsonContext.Default.JsonRpcStreamFrame,
            cancellationToken);

    private async Task WriteFrameAsync<T>(
        T frame, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, typeInfo);
        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}
