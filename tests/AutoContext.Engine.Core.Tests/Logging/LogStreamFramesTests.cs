namespace AutoContext.Engine.Core.Tests.Logging;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

public sealed class LogStreamFramesTests
{
    [Fact]
    public async Task Should_yield_records_in_order_as_LogRecordFrames()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonLogRecord>();
        var first = LogRecordFakeData.CreateLogRecord(message: "first");
        var second = LogRecordFakeData.CreateLogRecord(message: "second");

        Assert.True(channel.Writer.TryWrite(first));
        Assert.True(channel.Writer.TryWrite(second));
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonLogRecord>(
            channel.Reader,
            release: () => { },
            wasEvicted: () => false);

        // Act
        var frames = new List<JsonLogStreamFrame>();
        await foreach (var frame in LogStreamFrames.MapAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert — two record frames in FIFO order, no terminal.
        Assert.Multiple(
            () => Assert.Equal(2, frames.Count),
            () => Assert.Same(first, Assert.IsType<JsonLogRecordFrame>(frames[0]).Record),
            () => Assert.Same(second, Assert.IsType<JsonLogRecordFrame>(frames[1]).Record));
    }

    [Fact]
    public async Task Should_not_yield_terminal_frame_when_not_evicted()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonLogRecord>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonLogRecord>(
            channel.Reader,
            release: () => { },
            wasEvicted: () => false);

        // Act
        var frames = new List<JsonLogStreamFrame>();
        await foreach (var frame in LogStreamFrames.MapAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert
        Assert.Empty(frames);
    }

    [Fact]
    public async Task Should_yield_terminal_evicted_frame_when_wasEvicted_is_true()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonLogRecord>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonLogRecord>(
            channel.Reader,
            release: () => { },
            wasEvicted: () => true);

        // Act
        var frames = new List<JsonLogStreamFrame>();
        await foreach (var frame in LogStreamFrames.MapAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert
        var terminal = Assert.IsType<JsonLogEvictedFrame>(Assert.Single(frames));
        Assert.Equal(JsonLogEvictedFrame.SlowSubscriberReason, terminal.Reason);
    }
}
