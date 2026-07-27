namespace AutoContext.Client.Core.Engine.Rpc;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
    private int _disposed;
    private readonly SemaphoreSlim _exchangeGate = new(1, 1);
    private int _nextRequestId;
    private readonly Stream _stream;

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

    /// <summary>
    /// Sends a unary request and deserialises the success result into
    /// <typeparamref name="TResult"/>. A JSON-RPC error response, a
    /// missing result, or an unparsable result all surface as an
    /// <see cref="EngineRpcException"/>.
    /// </summary>
    /// <typeparam name="TResult">Result DTO to deserialise into.</typeparam>
    /// <param name="method">JSON-RPC method name.</param>
    /// <param name="parameters">Opaque params payload, or
    /// <see langword="null"/> for a parameter-less request.</param>
    /// <param name="resultTypeInfo">Source-generated type info for
    /// <typeparamref name="TResult"/>.</param>
    /// <param name="cancellationToken">Cancellation for the exchange.</param>
    /// <exception cref="EngineRpcException">The engine returned an error
    /// response, no result, or an unparsable result.</exception>
    public async Task<TResult> InvokeAsync<TResult>(
        string method,
        JsonElement? parameters,
        JsonTypeInfo<TResult> resultTypeInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultTypeInfo);

        var response = await ExchangeAsync(method, parameters, cancellationToken).ConfigureAwait(false);

        if (response.Error is { } error)
        {
            throw new EngineRpcException(method, error.Code, error.Message);
        }

        if (response.Result is not { } result)
        {
            throw new EngineRpcException(method, "the engine returned a success response with no result.");
        }

        TResult? value;
        try
        {
            value = result.Deserialize(resultTypeInfo);
        }
        catch (JsonException ex)
        {
            throw new EngineRpcException(method, "the engine returned an unparsable result.", ex);
        }

        return value ?? throw new EngineRpcException(method, "the engine returned a null result.");
    }

    /// <summary>
    /// Reads server-pushed JSON-RPC notification frames off a passive
    /// broadcast pipe (e.g. <c>events</c>) until the engine completes
    /// the connection. Unlike <see cref="SubscribeAsync"/> it writes no
    /// subscribe request — binding the pipe and completing the handshake
    /// is itself the subscription — and monopolises the read side, so
    /// callers give each notification stream a dedicated connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the stream.</param>
    /// <exception cref="EngineProtocolException">The engine sent an
    /// unparsable notification frame.</exception>
    public async IAsyncEnumerable<JsonRpcNotification> ReceiveNotificationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        while (true)
        {
            var bytes = await _codec.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                yield break;
            }

            yield return ParseNotification(bytes);
        }
    }

    /// <summary>
    /// Writes <paramref name="method"/> as one fire-and-forget JSON-RPC
    /// notification frame — no <c>id</c>, no response awaited. Serialised
    /// against the exchange gate so a notification never interleaves on
    /// the wire with an in-flight unary request on the same connection.
    /// </summary>
    /// <param name="method">JSON-RPC method name. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="parameters">Opaque params payload, or
    /// <see langword="null"/> for a parameter-less notification.</param>
    /// <param name="cancellationToken">Cancellation for the write.</param>
    /// <exception cref="ArgumentException"><paramref name="method"/> is
    /// <see langword="null"/> or empty.</exception>
    /// <exception cref="ObjectDisposedException">The connection has
    /// been disposed.</exception>
    public async Task SendNotificationAsync(
        string method, JsonElement? parameters, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var notification = new JsonRpcNotification
        {
            JsonRpc = JsonRpcVersion.Value,
            Method = method,
            Params = parameters,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            notification, ProtocolJsonContext.Default.JsonRpcNotification);

        await _exchangeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _exchangeGate.Release();
        }
    }

    /// <summary>
    /// Opens a server-streaming subscription and yields each
    /// <see cref="JsonRpcStreamNext.Result"/> payload until the engine
    /// completes the stream (clean end) or faults it. A subscription
    /// monopolises this connection's read side, so callers give each
    /// subscription a dedicated connection rather than sharing the
    /// unary one.
    /// </summary>
    /// <param name="method">Subscription method name.</param>
    /// <param name="parameters">Opaque params payload, or
    /// <see langword="null"/> for a parameter-less subscription.</param>
    /// <param name="cancellationToken">Cancellation for the stream.</param>
    /// <exception cref="EngineRpcException">The engine terminated the
    /// stream with an error frame or sent an unparsable frame.</exception>
    public async IAsyncEnumerable<JsonElement> SubscribeAsync(
        string method,
        JsonElement? parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        await WriteRequestAsync(method, parameters, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var bytes = await _codec.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                yield break;
            }

            var frame = ParseStreamFrame(method, bytes);
            if (frame is JsonRpcStreamNext next)
            {
                yield return next.Result;
                continue;
            }

            if (frame is JsonRpcStreamError streamError)
            {
                throw new EngineRpcException(method, streamError.Error.Code, streamError.Error.Message);
            }

            yield break;
        }
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

    private static JsonRpcNotification ParseNotification(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize(bytes, ProtocolJsonContext.Default.JsonRpcNotification)
                ?? throw new EngineProtocolException(
                    "The engine sent an empty notification frame on a broadcast pipe.");
        }
        catch (JsonException ex)
        {
            throw new EngineProtocolException(
                "The engine sent an unparsable notification frame on a broadcast pipe.", ex);
        }
    }

    private static JsonRpcStreamFrame ParseStreamFrame(string method, byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize(bytes, ProtocolJsonContext.Default.JsonRpcStreamFrame)
                ?? throw new EngineRpcException(method, "the engine sent an empty stream frame.");
        }
        catch (JsonException ex)
        {
            throw new EngineRpcException(method, "the engine sent an unparsable stream frame.", ex);
        }
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
