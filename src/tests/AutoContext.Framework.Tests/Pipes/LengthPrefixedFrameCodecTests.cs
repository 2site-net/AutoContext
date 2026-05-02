namespace AutoContext.Framework.Tests.Pipes;

using System.Buffers.Binary;

using AutoContext.Framework.Pipes;

public sealed class LengthPrefixedFrameCodecTests
{
    [Fact]
    public async Task Should_round_trip_a_single_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream();
        var codec = new LengthPrefixedFrameCodec(stream);
        var payload = "hello"u8.ToArray();

        await codec.WriteAsync(payload, cancellationToken);
        stream.Position = 0;

        var result = await codec.ReadAsync(cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task Should_return_null_when_stream_ends_before_header()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream();
        var codec = new LengthPrefixedFrameCodec(stream);

        var result = await codec.ReadAsync(cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_return_null_when_stream_ends_mid_payload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = new byte[4 + 3];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 10);
        "abc"u8.CopyTo(bytes.AsSpan(4));
        using var stream = new MemoryStream(bytes);
        var codec = new LengthPrefixedFrameCodec(stream);

        var result = await codec.ReadAsync(cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_return_empty_array_for_zero_length_payload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(0));
        var codec = new LengthPrefixedFrameCodec(stream);

        var result = await codec.ReadAsync(cancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_throw_when_announced_length_exceeds_max()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(LengthPrefixedFrameCodec.MaxMessageBytes + 1));
        var codec = new LengthPrefixedFrameCodec(stream);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await codec.ReadAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_throw_when_announced_length_is_negative()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(-1));
        var codec = new LengthPrefixedFrameCodec(stream);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            async () => await codec.ReadAsync(cancellationToken));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_handle_partial_reads_across_calls()
    {
        // A stream that returns only one byte per ReadAsync call exercises
        // the read-exact loop in the codec.
        var cancellationToken = TestContext.Current.CancellationToken;
        var payload = "partial"u8.ToArray();
        var framed = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(framed, payload.Length);
        payload.CopyTo(framed.AsSpan(4));

        using var stream = new ChunkedReadStream(framed, chunkSize: 1);
        var codec = new LengthPrefixedFrameCodec(stream);

        var result = await codec.ReadAsync(cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(payload, result);
    }

    private static byte[] WriteHeader(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        return header;
    }

    private sealed class ChunkedReadStream(byte[] buffer, int chunkSize) : Stream
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
}
