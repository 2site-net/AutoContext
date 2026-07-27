namespace AutoContext.Client.Core.Tests.Support.Engine.Rpc;

using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// A connected pipe pair for the connection- and client-level tests: the
/// client end is wrapped as an <see cref="EngineConnection"/> the code
/// under test drives, while the server end is scripted directly through
/// the read / write helpers. The client dials through the production
/// <see cref="PipeTransport"/>; the single-connection server end stays a
/// bare <see cref="NamedPipeServerStream"/> because the listener's
/// multi-accept machinery buys nothing for a one-shot, inline-driven
/// pair. It skips the connector, spawner, and handshake so a test can
/// exercise the raw request / response, streaming, and notification
/// framing in isolation.
/// </summary>
internal sealed class EnginePipePair : IAsyncDisposable
{
    private readonly LengthPrefixedFrameCodec _serverCodec;
    private readonly NamedPipeServerStream _serverStream;

    private EnginePipePair(NamedPipeServerStream serverStream, Stream clientStream)
    {
        _serverStream = serverStream;
        _serverCodec = new LengthPrefixedFrameCodec(serverStream);
        ClientConnection = new EngineConnection(clientStream);
    }

    /// <summary>The client end, wrapped for the code under test.</summary>
    public EngineConnection ClientConnection { get; }

    /// <summary>Binds a fresh pipe pair and connects both ends.</summary>
    public static async Task<EnginePipePair> CreateAsync(CancellationToken cancellationToken)
    {
        var name = "ac-pair-" + Guid.NewGuid().ToString("N");
        var serverStream = new NamedPipeServerStream(
            name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);
        var waitForConnection = serverStream.WaitForConnectionAsync(cancellationToken);
        var clientStream = await transport
            .ConnectAsync(name, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await waitForConnection.ConfigureAwait(false);

        return new EnginePipePair(serverStream, clientStream);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ClientConnection.DisposeAsync().ConfigureAwait(false);
        await _serverStream.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Reads the next frame the client wrote as a request.</summary>
    public async Task<JsonRpcRequest> ReadRequestAsync(CancellationToken cancellationToken)
    {
        var bytes = await _serverCodec.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The client closed the connection before sending a frame.");

        return JsonSerializer.Deserialize(bytes, ProtocolJsonContext.Default.JsonRpcRequest)
            ?? throw new InvalidOperationException("The client sent an empty frame.");
    }

    /// <summary>
    /// Reads the next request and answers it with an empty <c>{}</c>
    /// success result, returning the request for assertion.
    /// </summary>
    public async Task<JsonRpcRequest> ReadRequestAndRespondEmptyAsync(CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(cancellationToken).ConfigureAwait(false);
        using var empty = JsonDocument.Parse("{}");
        await WriteResponseAsync(request.Id, empty.RootElement.Clone(), cancellationToken).ConfigureAwait(false);
        return request;
    }

    /// <summary>Writes a JSON-RPC error response echoing <paramref name="id"/>.</summary>
    public Task WriteErrorAsync(
        JsonElement id, int code, string message, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcResponse { Id = id, Error = new JsonRpcError { Code = code, Message = message } },
            ProtocolJsonContext.Default.JsonRpcResponse,
            cancellationToken);

    /// <summary>Pushes a server-side JSON-RPC notification frame.</summary>
    public Task WriteNotificationAsync(
        string method, JsonElement? parameters, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcNotification { Method = method, Params = parameters },
            ProtocolJsonContext.Default.JsonRpcNotification,
            cancellationToken);

    /// <summary>
    /// Writes a JSON-RPC success response echoing <paramref name="id"/>.
    /// A <see langword="null"/> <paramref name="result"/> writes a
    /// success frame with the <c>result</c> field absent.
    /// </summary>
    public Task WriteResponseAsync(
        JsonElement id, JsonElement? result, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcResponse { Id = id, Result = result },
            ProtocolJsonContext.Default.JsonRpcResponse,
            cancellationToken);

    /// <summary>Writes the terminal <c>complete</c> stream frame.</summary>
    public Task WriteStreamCompleteAsync(JsonElement id, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcStreamComplete { Id = id },
            ProtocolJsonContext.Default.JsonRpcStreamFrame,
            cancellationToken);

    /// <summary>Writes a terminal <c>error</c> stream frame.</summary>
    public Task WriteStreamErrorAsync(
        JsonElement id, int code, string message, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcStreamError { Id = id, Error = new JsonRpcError { Code = code, Message = message } },
            ProtocolJsonContext.Default.JsonRpcStreamFrame,
            cancellationToken);

    /// <summary>Writes one <c>next</c> stream frame carrying <paramref name="result"/>.</summary>
    public Task WriteStreamNextAsync(JsonElement id, JsonElement result, CancellationToken cancellationToken)
        => WriteFrameAsync(
            new JsonRpcStreamNext { Id = id, Result = result },
            ProtocolJsonContext.Default.JsonRpcStreamFrame,
            cancellationToken);

    private async Task WriteFrameAsync<T>(
        T frame, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, typeInfo);
        await _serverCodec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}
