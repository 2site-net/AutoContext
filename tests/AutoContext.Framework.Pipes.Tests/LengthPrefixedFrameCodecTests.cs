namespace AutoContext.Framework.Pipes.Tests;

using System.Buffers.Binary;

using AutoContext.Framework.Pipes;
using AutoContext.Framework.Tests.Support.Pipes;

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
        using var stream = new MemoryStream(FrameHeaderTestEncoder.Encode(0));
        var codec = new LengthPrefixedFrameCodec(stream);

        var result = await codec.ReadAsync(cancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_throw_when_announced_length_exceeds_max()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(FrameHeaderTestEncoder.Encode(LengthPrefixedFrameCodec.MaxMessageBytes + 1));
        var codec = new LengthPrefixedFrameCodec(stream);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await codec.ReadAsync(cancellationToken));
    }

    [Fact]
    public async Task Should_throw_when_announced_length_is_negative()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(FrameHeaderTestEncoder.Encode(-1));
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
}
