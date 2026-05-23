namespace AutoContext.Framework.Tests.Support.Pipes;

/// <summary>
/// Read-only <see cref="Stream"/> wrapper that hands out at most
/// <c>chunkSize</c> bytes per <c>Read</c>/<c>ReadAsync</c> call.
/// Used by framing-codec tests to exercise the read-exact loop with
/// deliberately fragmented underlying reads.
/// </summary>
public sealed class ChunkedReadStream(byte[] buffer, int chunkSize) : Stream
{
    private readonly byte[] _buffer = buffer;
    private readonly int _chunkSize = chunkSize;
    private int _position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _buffer.Length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var available = Math.Min(_buffer.Length - _position, Math.Min(count, _chunkSize));
        if (available <= 0) { return 0; }
        Array.Copy(_buffer, _position, buffer, offset, available);
        _position += available;
        return available;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var available = Math.Min(_buffer.Length - _position, Math.Min(buffer.Length, _chunkSize));
        if (available <= 0) { return new ValueTask<int>(0); }
        _buffer.AsSpan(_position, available).CopyTo(buffer.Span);
        _position += available;
        return new ValueTask<int>(available);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
