namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

public sealed class LogSubscriptionBroadcasterTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => new LogSubscriptionBroadcaster(null!));
    }

    [Fact]
    public void Should_throw_when_publishing_null_record()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => broadcaster.TryPublish(null!));
    }

    [Fact]
    public void Should_be_idempotent_when_complete_is_called_twice()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();

        // Act + Assert — second call must not throw.
        broadcaster.Complete();
        broadcaster.Complete();
    }

    [Fact]
    public void Should_return_false_when_publishing_after_complete()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();
        var record = LogRecordFakeData.CreateLogRecord();
        broadcaster.Complete();

        // Act
        var accepted = broadcaster.TryPublish(record);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task Should_return_immediately_completed_subscription_after_complete()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();
        broadcaster.Complete();

        // Act
        using var subscription = broadcaster.Subscribe();
        var frames = await LogSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — no records, no terminal frame (graceful shutdown
        // is not an eviction).
        Assert.Empty(frames);
    }

    [Fact]
    public async Task Should_fan_out_record_to_every_subscriber()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();
        using var first = broadcaster.Subscribe();
        using var second = broadcaster.Subscribe();
        var record = LogRecordFakeData.CreateLogRecord(message: "shared");

        // Act
        Assert.True(broadcaster.TryPublish(record));
        broadcaster.Complete();

        var firstFrames = await LogSubscriptionTestDrainer.DrainAsync(first);
        var secondFrames = await LogSubscriptionTestDrainer.DrainAsync(second);

        // Assert — both subscribers see the same record, neither
        // sees a terminal frame.
        Assert.Multiple(
            () => Assert.Same(record, Assert.IsType<LogRecordFrame>(Assert.Single(firstFrames)).Record),
            () => Assert.Same(record, Assert.IsType<LogRecordFrame>(Assert.Single(secondFrames)).Record));
    }

    [Fact]
    public async Task Should_evict_slow_subscriber_with_terminal_frame_on_overflow()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();
        using var slow = broadcaster.Subscribe();

        // Act — publish capacity + 1 records without draining.
        // The (capacity+1)-th publish triggers eviction.
        for (var i = 0; i <= LogSubscriptionBroadcaster.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(
                LogRecordFakeData.CreateLogRecord(message: $"r{i}")));
        }

        broadcaster.Complete();
        var frames = await LogSubscriptionTestDrainer.DrainAsync(slow);

        // Assert — buffered records (up to capacity) plus the
        // terminal evicted frame as the very last frame.
        var terminal = Assert.IsType<LogEvictedFrame>(frames[^1]);
        Assert.Multiple(
            () => Assert.Equal(LogEvictedFrame.SlowSubscriberReason, terminal.Reason),
            () => Assert.Equal(LogSubscriptionBroadcaster.SubscriberBufferCapacity, frames.Count - 1),
            () => Assert.All(frames.Take(frames.Count - 1), frame => Assert.IsType<LogRecordFrame>(frame)));
    }

    [Fact]
    public async Task Should_keep_survivor_flowing_when_sibling_is_evicted()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();
        using var slow = broadcaster.Subscribe();
        using var fast = broadcaster.Subscribe();

        await using var fastEnumerator = fast
            .ReadAllAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        // Act — interleave publish + fast-side drain so 'fast'
        // never fills, while 'slow' is starved and overflows.
        var record = LogRecordFakeData.CreateLogRecord(message: "shared");
        for (var i = 0; i <= LogSubscriptionBroadcaster.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(record));
            Assert.True(await fastEnumerator.MoveNextAsync());
            Assert.IsType<LogRecordFrame>(fastEnumerator.Current);
        }

        broadcaster.Complete();
        var slowFrames = await LogSubscriptionTestDrainer.DrainAsync(slow);

        // Drain whatever 'fast' has left after Complete (should be
        // nothing — every record was consumed in lock-step — and
        // no terminal evicted frame because fast kept pace).
        var fastTrailing = new List<LogStreamFrame>();
        while (await fastEnumerator.MoveNextAsync())
        {
            fastTrailing.Add(fastEnumerator.Current);
        }

        // Assert — slow received its terminal evicted frame; fast
        // observed no terminal evicted frame after Complete.
        Assert.Multiple(
            () => Assert.IsType<LogEvictedFrame>(slowFrames[^1]),
            () => Assert.DoesNotContain(fastTrailing, frame => frame is LogEvictedFrame));
    }

    [Fact]
    public async Task Should_complete_active_subscription_without_terminal_frame_on_complete()
    {
        // Arrange
        var broadcaster = LogSubscriptionBroadcasterTestFactory.Create();
        using var subscription = broadcaster.Subscribe();
        var record = LogRecordFakeData.CreateLogRecord();
        Assert.True(broadcaster.TryPublish(record));

        // Act
        broadcaster.Complete();
        var frames = await LogSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the record plus EOF, no terminal evicted frame.
        Assert.Multiple(
            () => Assert.Same(record, Assert.IsType<LogRecordFrame>(Assert.Single(frames)).Record));
    }
}
