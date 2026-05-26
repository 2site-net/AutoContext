namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

public sealed class LogChannelTests
{
    [Fact]
    public void Should_throw_when_writing_null_record()
    {
        // Arrange
        var channel = new LogChannel();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => channel.TryWrite(null!));
    }

    [Fact]
    public async Task Should_enqueue_records_for_reader_in_FIFO_order()
    {
        // Arrange
        var channel = new LogChannel();
        var first = LogRecordFakeData.CreateLogRecord(message: "first");
        var second = LogRecordFakeData.CreateLogRecord(message: "second");

        // Act
        Assert.True(channel.TryWrite(first));
        Assert.True(channel.TryWrite(second));
        channel.Complete();

        // Assert
        var drained = new List<LogRecord>();
        await foreach (var record in channel.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            drained.Add(record);
        }

        Assert.Multiple(
            () => Assert.Equal(2, drained.Count),
            () => Assert.Same(first, drained[0]),
            () => Assert.Same(second, drained[1]));
    }

    [Fact]
    public async Task Should_drop_oldest_records_when_channel_is_full()
    {
        // Arrange — write one more than the channel's capacity so
        // DropOldest evicts exactly the first record. No
        // test-only capacity seam: the burst exercises the
        // production-sized channel directly.
        var channel = new LogChannel();
        var records = new LogRecord[LogChannel.DefaultCapacity + 1];

        for (var i = 0; i < records.Length; i++)
        {
            records[i] = LogRecordFakeData.CreateLogRecord(message: $"record-{i}");
        }

        // Act
        foreach (var record in records)
        {
            Assert.True(channel.TryWrite(record));
        }

        channel.Complete();

        // Assert — the very first record was evicted; everything
        // else drained in order.
        var drained = new List<LogRecord>();
        await foreach (var record in channel.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            drained.Add(record);
        }

        Assert.Multiple(
            () => Assert.Equal(LogChannel.DefaultCapacity, drained.Count),
            () => Assert.Same(records[1], drained[0]),
            () => Assert.Same(records[^1], drained[^1]));
    }

    [Fact]
    public void Should_reject_writes_after_complete()
    {
        // Arrange
        var channel = new LogChannel();
        channel.Complete();

        // Act + Assert
        Assert.False(channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "late")));
    }

    [Fact]
    public void Should_be_idempotent_when_complete_is_called_twice()
    {
        // Arrange
        var channel = new LogChannel();

        // Act + Assert — second call must not throw.
        channel.Complete();
        channel.Complete();
    }
}
