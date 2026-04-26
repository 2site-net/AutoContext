namespace AutoContext.Worker.Hosting;

using System.Buffers.Binary;

/// <summary>
/// Length-prefixed binary framing helpers for the worker pipe protocol:
/// 4-byte little-endian payload length followed by that many UTF-8 JSON bytes.
/// </summary>
public static class PipeFraming
{
    /// <summary>
    /// Maximum payload size accepted by <see cref="ReadMessageAsync"/>.
    /// Caps allocation when a corrupted or hostile header arrives; tasks
    /// exchanged on this pipe are small JSON envelopes well below this limit.
    /// </summary>
    public const int MaxMessageBytes = 64 * 1024 * 1024; // 64 MiB

    /// <summary>
    /// Reads one length-prefixed message from <paramref name="stream"/>.
    /// Returns <see langword="null"/> when the connection is closed before
    /// a full header is received.
    /// </summary>
    public static async Task<byte[]?> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[4];
        var headerRead = await ReadExactAsync(stream, header, ct).ConfigureAwait(false);

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
        var payloadRead = await ReadExactAsync(stream, payload, ct).ConfigureAwait(false);

        return payloadRead ? payload : null;
    }

    /// <summary>
    /// Writes <paramref name="payload"/> as one length-prefixed message.
    /// </summary>
    public static async Task WriteMessageAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(payload);

        var message = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(message, payload.Length);
        payload.CopyTo(message.AsSpan(4));

        await stream.WriteAsync(message, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
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
