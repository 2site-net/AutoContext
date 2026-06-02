namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

[Trait("Category", "Integration")]
public sealed class LogStreamTests
{
    [Fact]
    public async Task Should_yield_buffered_records_then_terminal_evicted_frame_on_overflow()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create<JsonLogRecord>("logs-pipe");
        using var slow = broadcaster.Subscribe();

        // Act — overflow a real broadcaster so it evicts the slow
        // subscriber, then drain it through the real framer.
        for (var i = 0; i <= Broadcaster<JsonLogRecord>.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(
                LogRecordFakeData.CreateLogRecord(message: $"r{i}")));
        }

        broadcaster.Complete();
        var frames = await LogStreamTestDrainer.DrainAsync(slow);

        // Assert — buffered record frames up to capacity, then the
        // terminal evicted frame as the very last frame.
        var terminal = Assert.IsType<JsonLogEvictedFrame>(frames[^1]);
        Assert.Multiple(
            () => Assert.Equal(JsonLogEvictedFrame.SlowSubscriberReason, terminal.Reason),
            () => Assert.Equal(Broadcaster<JsonLogRecord>.SubscriberBufferCapacity, frames.Count - 1),
            () => Assert.All(frames.Take(frames.Count - 1), frame => Assert.IsType<JsonLogRecordFrame>(frame)));
    }

    [Fact]
    public async Task Should_keep_survivor_flowing_when_sibling_is_evicted()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create<JsonLogRecord>("logs-pipe");
        using var slow = broadcaster.Subscribe();
        using var fast = broadcaster.Subscribe();

        await using var fastEnumerator = LogStreamFrames
            .MapAsync(fast, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        // Act — interleave publish + fast-side drain so 'fast' never
        // fills, while 'slow' is starved and overflows.
        var record = LogRecordFakeData.CreateLogRecord(message: "shared");
        for (var i = 0; i <= Broadcaster<JsonLogRecord>.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(record));
            Assert.True(await fastEnumerator.MoveNextAsync());
            Assert.IsType<JsonLogRecordFrame>(fastEnumerator.Current);
        }

        broadcaster.Complete();
        var slowFrames = await LogStreamTestDrainer.DrainAsync(slow);

        var fastTrailing = new List<JsonLogStreamFrame>();
        while (await fastEnumerator.MoveNextAsync())
        {
            fastTrailing.Add(fastEnumerator.Current);
        }

        // Assert — slow received its terminal evicted frame; fast
        // kept pace and observed no terminal evicted frame.
        Assert.Multiple(
            () => Assert.IsType<JsonLogEvictedFrame>(slowFrames[^1]),
            () => Assert.DoesNotContain(fastTrailing, frame => frame is JsonLogEvictedFrame));
    }
}
