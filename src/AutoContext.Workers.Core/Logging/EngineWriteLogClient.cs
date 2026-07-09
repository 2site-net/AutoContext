namespace AutoContext.Workers.Core.Logging;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Worker-side typed client for the <c>Engine.WriteLog</c> RPC. Holds
/// one persistent connection to the engine's <c>rpc</c> pipe: it dials
/// the pipe, completes the mandatory <c>Engine.Hello</c> handshake
/// (validating the protocol version), then ships each
/// <see cref="JsonLogRecord"/> as a fire-and-forget JSON-RPC 2.0
/// notification — no <c>id</c>, no response — on that same connection.
/// </summary>
/// <remarks>
/// <para>
/// The client is a best-effort transport: <see cref="TrySendAsync"/>
/// reports failure by returning <see langword="false"/> rather than
/// throwing, and drops the underlying connection so the next call
/// re-dials. Buffering, retry pacing, and the drop-oldest overflow
/// policy live in <see cref="EngineLogIngestRing"/>, which owns the
/// single caller of this client; the client itself carries no queue.
/// </para>
/// <para>
/// An empty (or otherwise unusable) engine address disables the
/// client — every <see cref="TrySendAsync"/> returns
/// <see langword="false"/> so the ring routes drops to its stderr
/// fallback. This keeps standalone worker runs (no engine parent)
/// diagnosable without the call site special-casing the address.
/// </para>
/// </remarks>
public sealed partial class EngineWriteLogClient : IAsyncDisposable
{
    private const int ConnectTimeoutMs = 2000;
    private const int HandshakeTimeoutMs = 5000;

    private static readonly JsonElement HelloRequestId = JsonDocument.Parse("1").RootElement.Clone();

    private LengthPrefixedFrameCodec? _codec;
    private int _disposed;
    private readonly string _engineRpcAddress;
    private readonly ILogger<EngineWriteLogClient> _logger;
    private Stream? _stream;
    private readonly PipeTransport _transport;

    /// <summary>
    /// Creates a new client targeting the engine's <c>rpc</c>
    /// endpoint.
    /// </summary>
    /// <param name="engineRpcAddress">Named-pipe address of the
    /// engine's <c>rpc</c> endpoint. Empty disables the client (every
    /// send fails so the ring falls back to stderr).</param>
    /// <param name="logger">Logger for connection diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="engineRpcAddress"/> or <paramref name="logger"/>
    /// is <see langword="null"/>.</exception>
    public EngineWriteLogClient(string engineRpcAddress, ILogger<EngineWriteLogClient> logger)
    {
        ArgumentNullException.ThrowIfNull(engineRpcAddress);
        ArgumentNullException.ThrowIfNull(logger);

        _engineRpcAddress = engineRpcAddress;
        _logger = logger;
        _transport = new PipeTransport(NullLogger<PipeTransport>.Instance);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await DropConnectionAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <paramref name="record"/> to the engine as an
    /// <c>Engine.WriteLog</c> notification, dialling and handshaking
    /// first when not already connected. Never throws for a broken
    /// connection: a connect, handshake, or write failure returns
    /// <see langword="false"/> and drops the connection so the next
    /// call re-dials.
    /// </summary>
    /// <param name="record">The record to ship. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation for the connect,
    /// handshake, and write.</param>
    /// <returns><see langword="true"/> when the notification was
    /// written; <see langword="false"/> when the client is disabled,
    /// disposed, or the engine was unreachable.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="record"/> is <see langword="null"/>.</exception>
    public async Task<bool> TrySendAsync(JsonLogRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (Volatile.Read(ref _disposed) != 0 || _engineRpcAddress.Length == 0)
        {
            return false;
        }

        if (_codec is null && !await TryConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var codec = _codec;

        if (codec is null)
        {
            return false;
        }

        try
        {
            await codec.WriteAsync(SerializeNotification(record), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            LogSendFailed(_logger, ex);
            await DropConnectionAsync().ConfigureAwait(false);
            return false;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Engine rpc pipe '{Address}' was unreachable for worker log delivery.")]
    private static partial void LogConnectFailed(ILogger logger, string address, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Engine.Hello handshake on rpc pipe '{Address}' timed out for worker log delivery.")]
    private static partial void LogHandshakeTimedOut(ILogger logger, string address);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Writing an Engine.WriteLog notification failed; dropping the engine connection.")]
    private static partial void LogSendFailed(ILogger logger, Exception exception);

    private static byte[] SerializeHello()
    {
        var paramsElement = JsonSerializer.SerializeToElement(
            new JsonHandshakeParams { ProtocolVersion = ProtocolVersion.Current },
            ProtocolJsonContext.Default.JsonHandshakeParams);

        var request = new JsonRpcRequest
        {
            Id = HelloRequestId,
            Method = ProtocolMethods.Hello,
            Params = paramsElement,
        };

        return JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);
    }

    private static byte[] SerializeNotification(JsonLogRecord record)
    {
        var paramsElement = JsonSerializer.SerializeToElement(
            record, ProtocolJsonContext.Default.JsonLogRecord);

        var notification = new JsonRpcNotification
        {
            Method = ProtocolMethods.WriteLog,
            Params = paramsElement,
        };

        return JsonSerializer.SerializeToUtf8Bytes(
            notification, ProtocolJsonContext.Default.JsonRpcNotification);
    }

    private static async Task<bool> TryHandshakeAsync(
        LengthPrefixedFrameCodec codec, CancellationToken cancellationToken)
    {
        await codec.WriteAsync(SerializeHello(), cancellationToken).ConfigureAwait(false);

        var responseBytes = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (responseBytes is null)
        {
            return false;
        }

        try
        {
            var response = JsonSerializer.Deserialize(
                responseBytes, ProtocolJsonContext.Default.JsonRpcResponse);

            if (response is null || response.Error is not null || response.Result is not { } result)
            {
                return false;
            }

            var handshake = result.Deserialize(ProtocolJsonContext.Default.JsonHandshakeResult);
            return handshake is not null && handshake.ProtocolVersion == ProtocolVersion.Current;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task DropConnectionAsync()
    {
        var stream = _stream;
        _stream = null;
        _codec = null;

        if (stream is not null)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> TryConnectAsync(CancellationToken cancellationToken)
    {
        Stream? stream = null;
        var connected = false;

        try
        {
            stream = await _transport
                .ConnectAsync(_engineRpcAddress, ConnectTimeoutMs, PipeDirection.InOut, cancellationToken)
                .ConfigureAwait(false);
            var codec = new LengthPrefixedFrameCodec(stream);

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeCts.CancelAfter(HandshakeTimeoutMs);

            if (await TryHandshakeAsync(codec, handshakeCts.Token).ConfigureAwait(false))
            {
                _stream = stream;
                _codec = codec;
                connected = true;
                return true;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The handshake read exceeded HandshakeTimeoutMs — the
            // engine accepted the connection but never completed
            // Engine.Hello. Treat as unreachable, not a caller cancel.
            LogHandshakeTimedOut(_logger, _engineRpcAddress);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A malformed address (ArgumentException) is a spawn-time
            // misconfiguration, but the logging path must never fault
            // the worker — surface it as an unreachable engine and let
            // the ring keep retrying with backoff.
            LogConnectFailed(_logger, _engineRpcAddress, ex);
        }
        finally
        {
            if (!connected && stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        return false;
    }
}
