namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Protocol.Messages.Config;

[Trait("Category", "Integration")]
public sealed class ConfigStreamTests
{
    [Fact]
    public async Task Should_replay_primed_snapshot_as_first_frame()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create<JsonConfigSnapshot>("Config.Subscribe");
        var primed = new JsonConfigSnapshot { Version = "seed" };
        broadcaster.Prime(primed);

        // Act — a new subscriber is seeded with the cached snapshot
        // through the real framer.
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var frames = await ConfigStreamTestDrainer.DrainAsync(subscription);

        // Assert — the primed snapshot is replayed as the first
        // (and only) frame, with no terminal evicted frame.
        Assert.Same(primed, Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(frames)).Snapshot);
    }

    [Fact]
    public async Task Should_yield_buffered_snapshots_then_terminal_evicted_frame_on_overflow()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create<JsonConfigSnapshot>("Config.Subscribe");
        using var slow = broadcaster.Subscribe();

        // Act — overflow a real snapshot broadcaster so it evicts the
        // slow subscriber, then drain it through the real framer.
        for (var i = 0; i <= Broadcaster<JsonConfigSnapshot>.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(new JsonConfigSnapshot { Version = $"v{i}" }));
        }

        broadcaster.Complete();
        var frames = await ConfigStreamTestDrainer.DrainAsync(slow);

        // Assert — buffered snapshot frames up to capacity, then the
        // terminal evicted frame as the very last frame.
        var terminal = Assert.IsType<JsonConfigEvictedFrame>(frames[^1]);
        Assert.Multiple(
            () => Assert.Equal(JsonConfigEvictedFrame.SlowSubscriberReason, terminal.Reason),
            () => Assert.Equal(Broadcaster<JsonConfigSnapshot>.SubscriberBufferCapacity, frames.Count - 1),
            () => Assert.All(frames.Take(frames.Count - 1), frame => Assert.IsType<JsonConfigSnapshotFrame>(frame)));
    }
}
