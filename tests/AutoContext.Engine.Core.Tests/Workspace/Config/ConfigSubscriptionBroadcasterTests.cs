namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config.Primitives;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Protocol.Messages.Config;

public sealed class ConfigSubscriptionBroadcasterTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => new ConfigSubscriptionBroadcaster(null!));
    }

    [Fact]
    public void Should_throw_when_priming_null_snapshot()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => broadcaster.Prime(null!));
    }

    [Fact]
    public void Should_throw_when_publishing_null_snapshot()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => broadcaster.TryPublish(null!));
    }

    [Fact]
    public void Should_be_idempotent_when_complete_is_called_twice()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();

        // Act + Assert — second call must not throw.
        broadcaster.Complete();
        broadcaster.Complete();
    }

    [Fact]
    public void Should_return_false_when_publishing_after_complete()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        broadcaster.Complete();

        // Act
        var accepted = broadcaster.TryPublish(new JsonConfigSnapshot { Version = "1.0" });

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task Should_seed_primed_snapshot_on_subscribe()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        var primed = new JsonConfigSnapshot { Version = "seed" };
        broadcaster.Prime(primed);

        // Act
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var frames = await ConfigSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the cached snapshot is replayed as the first
        // (and only) frame, with no terminal evicted frame.
        Assert.Same(primed, Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(frames)).Snapshot);
    }

    [Fact]
    public async Task Should_not_seed_when_nothing_primed()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();

        // Act
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var frames = await ConfigSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — no cached snapshot means no seed frame, just EOF.
        Assert.Empty(frames);
    }

    [Fact]
    public async Task Should_seed_latest_snapshot_after_publish()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        broadcaster.Prime(new JsonConfigSnapshot { Version = "stale" });
        var latest = new JsonConfigSnapshot { Version = "fresh" };
        Assert.True(broadcaster.TryPublish(latest));

        // Act — a late subscriber sees the most recent state, not
        // the stale primed seed.
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var frames = await ConfigSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert
        Assert.Same(latest, Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(frames)).Snapshot);
    }

    [Fact]
    public async Task Should_seed_then_complete_when_subscribing_after_complete()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        var primed = new JsonConfigSnapshot { Version = "seed" };
        broadcaster.Prime(primed);
        broadcaster.Complete();

        // Act
        using var subscription = broadcaster.Subscribe();
        var frames = await ConfigSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the seed lands ahead of the immediate EOF; no
        // terminal evicted frame (completion is not an eviction).
        Assert.Same(primed, Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(frames)).Snapshot);
    }

    [Fact]
    public async Task Should_fan_out_snapshot_to_every_subscriber()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        using var first = broadcaster.Subscribe();
        using var second = broadcaster.Subscribe();
        var snapshot = new JsonConfigSnapshot { Version = "shared" };

        // Act
        Assert.True(broadcaster.TryPublish(snapshot));
        broadcaster.Complete();

        var firstFrames = await ConfigSubscriptionTestDrainer.DrainAsync(first);
        var secondFrames = await ConfigSubscriptionTestDrainer.DrainAsync(second);

        // Assert — both subscribers see the same snapshot (neither
        // was primed, so the publish is their only frame).
        Assert.Multiple(
            () => Assert.Same(snapshot, Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(firstFrames)).Snapshot),
            () => Assert.Same(snapshot, Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(secondFrames)).Snapshot));
    }

    [Fact]
    public async Task Should_evict_slow_subscriber_with_terminal_frame_on_overflow()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        using var slow = broadcaster.Subscribe();

        // Act — publish capacity + 1 snapshots without draining.
        // The (capacity+1)-th publish triggers eviction.
        for (var i = 0; i <= ConfigSubscriptionBroadcaster.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(new JsonConfigSnapshot { Version = $"v{i}" }));
        }

        broadcaster.Complete();
        var frames = await ConfigSubscriptionTestDrainer.DrainAsync(slow);

        // Assert — buffered snapshots (up to capacity) plus the
        // terminal evicted frame as the very last frame.
        var terminal = Assert.IsType<JsonConfigEvictedFrame>(frames[^1]);
        Assert.Multiple(
            () => Assert.Equal(JsonConfigEvictedFrame.SlowSubscriberReason, terminal.Reason),
            () => Assert.Equal(ConfigSubscriptionBroadcaster.SubscriberBufferCapacity, frames.Count - 1),
            () => Assert.All(frames.Take(frames.Count - 1), frame => Assert.IsType<JsonConfigSnapshotFrame>(frame)));
    }

    [Fact]
    public async Task Should_keep_survivor_flowing_when_sibling_is_evicted()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        using var slow = broadcaster.Subscribe();
        using var fast = broadcaster.Subscribe();

        await using var fastEnumerator = fast
            .ReadAllAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        // Act — interleave publish + fast-side drain so 'fast'
        // never fills, while 'slow' is starved and overflows.
        var snapshot = new JsonConfigSnapshot { Version = "shared" };
        for (var i = 0; i <= ConfigSubscriptionBroadcaster.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(snapshot));
            Assert.True(await fastEnumerator.MoveNextAsync());
            Assert.IsType<JsonConfigSnapshotFrame>(fastEnumerator.Current);
        }

        broadcaster.Complete();
        var slowFrames = await ConfigSubscriptionTestDrainer.DrainAsync(slow);

        // Drain whatever 'fast' has left after Complete (should be
        // nothing — every snapshot was consumed in lock-step — and
        // no terminal evicted frame because fast kept pace).
        var fastTrailing = new List<JsonConfigStreamFrame>();
        while (await fastEnumerator.MoveNextAsync())
        {
            fastTrailing.Add(fastEnumerator.Current);
        }

        // Assert — slow received its terminal evicted frame; fast
        // observed no terminal evicted frame after Complete.
        Assert.Multiple(
            () => Assert.IsType<JsonConfigEvictedFrame>(slowFrames[^1]),
            () => Assert.DoesNotContain(fastTrailing, frame => frame is JsonConfigEvictedFrame));
    }

    [Fact]
    public async Task Should_complete_active_subscription_without_terminal_frame_on_complete()
    {
        // Arrange
        var broadcaster = ConfigSubscriptionBroadcasterTestFactory.Create();
        using var subscription = broadcaster.Subscribe();
        var snapshot = new JsonConfigSnapshot { Version = "1.0" };
        Assert.True(broadcaster.TryPublish(snapshot));

        // Act
        broadcaster.Complete();
        var frames = await ConfigSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the snapshot plus EOF, no terminal evicted frame.
        Assert.Same(snapshot, Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(frames)).Snapshot);
    }
}
