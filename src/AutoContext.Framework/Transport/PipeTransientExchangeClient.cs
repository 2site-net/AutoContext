namespace AutoContext.Framework.Transport;

using System.IO.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Request/response pipe client that opens a fresh
/// <see cref="NamedPipeClientStream"/> per <see cref="ExchangeAsync"/>
/// call and closes it on return. No state is shared between calls;
/// the type is safe to use from multiple threads concurrently.
/// </summary>
/// <remarks>
/// The connect attempt obeys <c>connectTimeoutMs</c> as well as the
/// per-call <see cref="CancellationToken"/>, so callers can layer an
/// outer overall deadline by combining the token with their own
/// linked <see cref="CancellationTokenSource"/>.
/// </remarks>
public sealed class PipeTransientExchangeClient : IPipeExchangeClient
{
    private readonly PipeTransport _transport;
    private readonly string _pipeName;
    private readonly int _connectTimeoutMs;

    /// <summary>
    /// Creates a new <see cref="PipeTransientExchangeClient"/>.
    /// </summary>
    /// <param name="transport">Connect primitive used for every
    /// <see cref="ExchangeAsync"/> call.</param>
    /// <param name="pipeName">Pipe name (without the
    /// <c>\\.\pipe\</c> prefix on Windows). Must be non-empty.</param>
    /// <param name="connectTimeoutMs">Connect timeout in milliseconds;
    /// pass <c>0</c> or a negative value to wait indefinitely subject
    /// to the per-call cancellation token.</param>
    public PipeTransientExchangeClient(
        PipeTransport transport,
        string pipeName,
        int connectTimeoutMs = 0)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentException.ThrowIfNullOrEmpty(pipeName);

        _transport = transport;
        _pipeName = pipeName;
        _connectTimeoutMs = connectTimeoutMs;
    }

    /// <inheritdoc />
    public async Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stream = await _transport.ConnectAsync(
            _pipeName, _connectTimeoutMs, PipeDirection.InOut, cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            var codec = new LengthPrefixedFrameCodec(stream);
            await codec.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            var response = await codec.ReadAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException(
                    $"Pipe '{_pipeName}' closed before sending a response.");

            return response;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
