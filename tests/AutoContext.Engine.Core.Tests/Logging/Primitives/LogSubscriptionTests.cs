namespace AutoContext.Engine.Core.Tests.Logging.Primitives;

using System.Threading.Channels;

using AutoContext.Engine.Core.Logging.Primitives;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

public sealed class LogSubscriptionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_reader()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LogSubscription(null!, release: () => { }, wasEvicted: () => false));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_release()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonLogRecord>();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LogSubscription(channel.Reader, release: null!, wasEvicted: () => false));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_wasEvicted()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonLogRecord>();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LogSubscription(channel.Reader, release: () => { }, wasEvicted: null!));
    }

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

        using var subscription = new LogSubscription(
            channel.Reader,
            release: () => { },
            wasEvicted: () => false);

        // Act
        var frames = new List<JsonLogStreamFrame>();
        await foreach (var frame in subscription.ReadAllAsync(TestContext.Current.CancellationToken))
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

        using var subscription = new LogSubscription(
            channel.Reader,
            release: () => { },
            wasEvicted: () => false);

        // Act
        var frames = new List<JsonLogStreamFrame>();
        await foreach (var frame in subscription.ReadAllAsync(TestContext.Current.CancellationToken))
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

        using var subscription = new LogSubscription(
            channel.Reader,
            release: () => { },
            wasEvicted: () => true);

        // Act
        var frames = new List<JsonLogStreamFrame>();
        await foreach (var frame in subscription.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert
        var terminal = Assert.IsType<JsonLogEvictedFrame>(Assert.Single(frames));
        Assert.Equal(JsonLogEvictedFrame.SlowSubscriberReason, terminal.Reason);
    }

    [Fact]
    public void Should_invoke_release_callback_on_dispose()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonLogRecord>();
        var releaseCount = 0;
        var subscription = new LogSubscription(
            channel.Reader,
            release: () => Interlocked.Increment(ref releaseCount),
            wasEvicted: () => false);

        // Act
        subscription.Dispose();

        // Assert
        Assert.Equal(1, releaseCount);
    }

    [Fact]
    public void Should_invoke_release_callback_exactly_once_when_disposed_twice()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonLogRecord>();
        var releaseCount = 0;
        var subscription = new LogSubscription(
            channel.Reader,
            release: () => Interlocked.Increment(ref releaseCount),
            wasEvicted: () => false);

        // Act
        subscription.Dispose();
        subscription.Dispose();

        // Assert
        Assert.Equal(1, releaseCount);
    }
}
