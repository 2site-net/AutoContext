namespace AutoContext.Framework.Workers;

using System.Buffers.Binary;

/// <summary>
/// Wire-protocol adapter for the worker pipe: 4-byte little-endian
/// payload length followed by that many UTF-8 JSON bytes. Wraps an
/// underlying <see cref="Stream"/> (typically a named-pipe stream) and
/// exposes message-oriented read/write operations on top of it.
/// </summary>
/// <remarks>
/// This is the contract counterpart of <c>WorkerProtocolChannel</c> in
/// <c>AutoContext.Worker.Web</c>; the two implementations are
/// bit-for-bit symmetric and must be changed together.
///
/// The wrapped stream's lifetime is owned by the caller — this type
/// neither closes nor disposes it.
/// </remarks>
public sealed class WorkerProtocolChannel
{
    /// <summary>
    /// Maximum payload size accepted by <see cref="ReadAsync"/>.
    /// Caps allocation when a corrupted or hostile header arrives;
    /// tasks exchanged on this pipe are small JSON envelopes well
    /// below this limit.
    /// </summary>
    public const int MaxMessageBytes = 64 * 1024 * 1024; // 64 MiB

    private readonly Stream _stream;

    /// <summary>
    /// Wraps <paramref name="stream"/> with the pipe wire protocol.
    /// The stream is not owned by this instance.
    /// </summary>
    public WorkerProtocolChannel(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
    }

    /// <summary>
    /// Reads one length-prefixed message from the wrapped stream.
    /// Returns <see langword="null"/> when the connection is closed
    /// before a full header is received.
    /// </summary>
    public async Task<byte[]?> ReadAsync(CancellationToken ct)
    {
        var header = new byte[4];
        var headerRead = await ReadExactAsync(header, ct).ConfigureAwait(false);

        if (!headerRead)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

        if (length < 0)
        {
            throw new InvalidDataException(
                $"Pipe message length {length} is negative; header is corrupt.");
        }

        if (length == 0)
        {
            return [];
        }

        if (length > MaxMessageBytes)
        {
            throw new InvalidDataException(
                $"Pipe message length {length} exceeds the maximum of {MaxMessageBytes} bytes.");
        }

        var payload = new byte[length];
        var payloadRead = await ReadExactAsync(payload, ct).ConfigureAwait(false);

        return payloadRead ? payload : null;
    }

    /// <summary>
    /// Writes <paramref name="payload"/> to the wrapped stream as one
    /// length-prefixed message.
    /// </summary>
    public async Task WriteAsync(byte[] payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var message = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(message, payload.Length);
        payload.CopyTo(message.AsSpan(4));

        await _stream.WriteAsync(message, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task<bool> ReadExactAsync(byte[] buffer, CancellationToken ct)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await _stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset), ct).ConfigureAwait(false);

            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
