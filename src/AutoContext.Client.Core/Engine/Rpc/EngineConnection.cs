namespace AutoContext.Client.Core.Engine.Rpc;

using System.Globalization;
using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

/// <summary>
/// One live, handshaked connection to an engine pipe. Owns the
/// underlying stream and its length-prefixed frame codec, mints
/// monotonic JSON-RPC request ids, and serialises unary exchanges so
/// one connection carries one in-flight request at a time. The typed
/// RPC clients marshal their per-method DTOs over
/// <see cref="ExchangeAsync"/>; subscription consumers hold a
/// dedicated connection each.
/// </summary>
public sealed class EngineConnection : IAsyncDisposable
{
    private readonly LengthPrefixedFrameCodec _codec;
    private readonly SemaphoreSlim _exchangeGate = new(1, 1);
    private readonly Stream _stream;
    private int _disposed;
    private int _nextRequestId;

    /// <summary>
    /// Wraps <paramref name="stream"/> as an engine connection. The
    /// connection owns the stream and disposes it on
    /// <see cref="DisposeAsync"/>.
    /// </summary>
    /// <param name="stream">Connected pipe stream. Must not be
    /// <see langword="null"/>.</param>
    internal EngineConnection(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
        _codec = new LengthPrefixedFrameCodec(stream);
    }

    /// <summary>
    /// Writes <paramref name="method"/> as one JSON-RPC request frame
    /// and returns the single response frame the engine writes back,
    /// serialised so concurrent callers take turns on the wire.
    /// </summary>
    /// <param name="method">JSON-RPC method name. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="parameters">Opaque params payload, or
    /// <see langword="null"/> for a parameter-less request.</param>
    /// <param name="cancellationToken">Cancellation for the exchange.</param>
    /// <exception cref="ArgumentException"><paramref name="method"/> is
    /// <see langword="null"/> or empty.</exception>
    /// <exception cref="ObjectDisposedException">The connection has
    /// been disposed.</exception>
    /// <exception cref="IOException">The engine closed the connection
    /// before responding, or the underlying stream faulted.</exception>
    public async Task<JsonRpcResponse> ExchangeAsync(
        string method, JsonElement? parameters, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        await _exchangeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteRequestAsync(method, parameters, cancellationToken).ConfigureAwait(false);
            return await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _exchangeGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stream.DisposeAsync().ConfigureAwait(false);
        _exchangeGate.Dispose();
    }

    /// <summary>
    /// Completes the mandatory <c>Engine.Hello</c> handshake, validating
    /// that the engine's protocol version exactly matches the client's.
    /// </summary>
    /// <exception cref="EngineProtocolException">The engine refused the
    /// handshake, returned an unparsable result, or reported a
    /// mismatched protocol version.</exception>
    internal async Task HandshakeAsync(CancellationToken cancellationToken)
    {
        var helloParams = JsonSerializer.SerializeToElement(
            new JsonHandshakeParams { ProtocolVersion = ProtocolVersion.Current },
            ProtocolJsonContext.Default.JsonHandshakeParams);

        JsonRpcResponse response;
        try
        {
            await WriteRequestAsync(ProtocolMethods.Hello, helloParams, cancellationToken)
                .ConfigureAwait(false);
            response = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            throw new EngineProtocolException(
                "The engine closed the connection during the Engine.Hello handshake.", ex);
        }

        if (response.Error is not null)
        {
            throw new EngineProtocolException(
                $"The engine rejected the Engine.Hello handshake: {response.Error.Message}");
        }

        if (response.Result is not { } result)
        {
            throw new EngineProtocolException(
                "The engine returned no result for the Engine.Hello handshake.");
        }

        JsonHandshakeResult? handshake;
        try
        {
            handshake = result.Deserialize(ProtocolJsonContext.Default.JsonHandshakeResult);
        }
        catch (JsonException ex)
        {
            throw new EngineProtocolException(
                "The engine returned an unparsable Engine.Hello result.", ex);
        }

        if (handshake is null || handshake.ProtocolVersion != ProtocolVersion.Current)
        {
            var reported = handshake?.ProtocolVersion.ToString(CultureInfo.InvariantCulture)
                ?? "an unknown version";
            throw new EngineProtocolException(
                $"Protocol version mismatch: engine reports {reported}, client requires "
                + $"{ProtocolVersion.Current.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static JsonElement CreateIdElement(int id)
    {
        using var document = JsonDocument.Parse(id.ToString(CultureInfo.InvariantCulture));
        return document.RootElement.Clone();
    }

    private async Task<JsonRpcResponse> ReadResponseAsync(CancellationToken cancellationToken)
    {
        var bytes = await _codec.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new IOException("The engine closed the connection before sending a response.");

        return JsonSerializer.Deserialize(bytes, ProtocolJsonContext.Default.JsonRpcResponse)
            ?? throw new IOException("The engine sent an empty response frame.");
    }

    private async Task WriteRequestAsync(
        string method, JsonElement? parameters, CancellationToken cancellationToken)
    {
        var request = new JsonRpcRequest
        {
            JsonRpc = JsonRpcVersion.Value,
            Id = CreateIdElement(Interlocked.Increment(ref _nextRequestId)),
            Method = method,
            Params = parameters,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);
        await _codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}
