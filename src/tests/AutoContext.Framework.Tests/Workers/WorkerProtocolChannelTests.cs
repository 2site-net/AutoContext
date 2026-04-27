namespace AutoContext.Framework.Tests.Workers;

using System.Buffers.Binary;

using AutoContext.Framework.Workers;

public sealed class WorkerProtocolChannelTests
{
    [Fact]
    public async Task Should_round_trip_a_single_message()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream();
        var channel = new WorkerProtocolChannel(stream);
        var payload = "hello"u8.ToArray();

        await channel.WriteAsync(payload, ct);
        stream.Position = 0;

        var result = await channel.ReadAsync(ct);

        Assert.NotNull(result);
        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task Should_return_null_when_stream_ends_before_header()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream();
        var channel = new WorkerProtocolChannel(stream);

        var result = await channel.ReadAsync(ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_return_empty_array_for_zero_length_payload()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(0));
        var channel = new WorkerProtocolChannel(stream);

        var result = await channel.ReadAsync(ct);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_throw_when_announced_length_exceeds_max()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(WorkerProtocolChannel.MaxMessageBytes + 1));
        var channel = new WorkerProtocolChannel(stream);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await channel.ReadAsync(ct));
    }

    [Fact]
    public async Task Should_throw_when_announced_length_is_negative()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stream = new MemoryStream(WriteHeader(-1));
        var channel = new WorkerProtocolChannel(stream);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            async () => await channel.ReadAsync(ct));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] WriteHeader(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        return header;
    }
}
