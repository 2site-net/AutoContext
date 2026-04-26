namespace AutoContext.Worker.Shared.Tests.Hosting;

using System.Buffers.Binary;

using AutoContext.Worker.Hosting;

public sealed class PipeFramingTests
{
    [Fact]
    public async Task Should_round_trip_a_single_message()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream();
        var payload = "hello"u8.ToArray();

        await PipeFraming.WriteMessageAsync(stream, payload, ct);
        stream.Position = 0;

        var result = await PipeFraming.ReadMessageAsync(stream, ct);

        Assert.NotNull(result);
        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task Should_return_null_when_stream_ends_before_header()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream();

        var result = await PipeFraming.ReadMessageAsync(stream, ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_return_empty_array_for_zero_length_payload()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(0));

        var result = await PipeFraming.ReadMessageAsync(stream, ct);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_throw_when_announced_length_exceeds_max()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(PipeFraming.MaxMessageBytes + 1));

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await PipeFraming.ReadMessageAsync(stream, ct));
    }

    [Fact]
    public async Task Should_throw_when_announced_length_is_negative()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(-1));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            async () => await PipeFraming.ReadMessageAsync(stream, ct));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] WriteHeader(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        return header;
    }
}
