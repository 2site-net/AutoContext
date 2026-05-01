namespace AutoContext.Framework.Transport;

/// <summary>
/// Layer-3 request/response pipe client: send one length-prefixed
/// frame, receive one length-prefixed frame, in strict turn-taking
/// order with the client speaking first.
/// </summary>
/// <remarks>
/// Two implementations share this contract:
/// <list type="bullet">
/// <item><see cref="PipeTransientExchangeClient"/> opens a fresh pipe
/// for every call and closes it on return.</item>
/// <item><see cref="PipePersistentExchangeClient"/> holds one open
/// pipe across calls and serializes them through an internal lock,
/// resetting the connection on any wire failure.</item>
/// </list>
/// Endpoint classes (<c>WorkerClient</c>, <c>WorkerControlClient</c>)
/// hold one of these and add domain-specific concerns
/// (deadline / coalescing / error envelope synthesis) on top.
/// </remarks>
public interface IPipeExchangeClient : IAsyncDisposable
{
    /// <summary>
    /// Writes <paramref name="request"/> as one length-prefixed
    /// frame and returns the next length-prefixed frame the peer
    /// writes back.
    /// </summary>
    /// <exception cref="IOException">The pipe was closed before a
    /// full response frame was received, or the underlying stream
    /// faulted during the exchange.</exception>
    /// <exception cref="TimeoutException">The connect attempt did
    /// not complete within the configured timeout.</exception>
    /// <exception cref="UnauthorizedAccessException">The current
    /// principal lacks permission to open the pipe.</exception>
    /// <exception cref="System.IO.InvalidDataException">The peer
    /// sent a frame with an invalid header.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before the
    /// exchange completed.</exception>
    Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken);
}
