namespace AutoContext.Engine.Core.Tests.Infrastructure.Events;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

public sealed class SnapshotBroadcasterTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new SnapshotBroadcaster<BroadcasterTestPayload>(null!, "test-channel"));
    }

    [Fact]
    public void Should_throw_when_priming_null_snapshot()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => broadcaster.Prime(null!));
    }

    [Fact]
    public void Should_throw_when_publishing_null_snapshot()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => broadcaster.TryPublish(null!));
    }

    [Fact]
    public void Should_be_idempotent_when_complete_is_called_twice()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();

        // Act + Assert — second call must not throw.
        broadcaster.Complete();
        broadcaster.Complete();
    }

    [Fact]
    public void Should_return_false_when_publishing_after_complete()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();
        broadcaster.Complete();

        // Act
        var accepted = broadcaster.TryPublish(new BroadcasterTestPayload(1));

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task Should_seed_primed_snapshot_on_subscribe()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();
        var primed = new BroadcasterTestPayload(1);
        broadcaster.Prime(primed);

        // Act
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the cached snapshot is replayed as the first
        // (and only) payload.
        Assert.Same(primed, Assert.Single(payloads));
    }

    [Fact]
    public async Task Should_not_seed_when_nothing_primed()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();

        // Act
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — no cached snapshot means no seed, just EOF.
        Assert.Empty(payloads);
    }

    [Fact]
    public async Task Should_seed_latest_snapshot_after_publish()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();
        broadcaster.Prime(new BroadcasterTestPayload(1));
        var latest = new BroadcasterTestPayload(2);
        Assert.True(broadcaster.TryPublish(latest));

        // Act — a late subscriber sees the most recent state, not
        // the stale primed seed.
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert
        Assert.Same(latest, Assert.Single(payloads));
    }

    [Fact]
    public async Task Should_seed_then_complete_when_subscribing_after_complete()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();
        var primed = new BroadcasterTestPayload(1);
        broadcaster.Prime(primed);
        broadcaster.Complete();

        // Act
        using var subscription = broadcaster.Subscribe();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the snapshot primed BEFORE completion lands ahead
        // of the immediate EOF.
        Assert.Same(primed, Assert.Single(payloads));
    }

    [Fact]
    public async Task Should_not_cache_snapshot_when_priming_after_complete()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();
        broadcaster.Complete();

        // Act — a prime that lands after completion must be ignored
        // so a late subscriber is not seeded from a dead stream.
        broadcaster.Prime(new BroadcasterTestPayload(1));
        using var subscription = broadcaster.Subscribe();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the post-completion prime did not cache; the
        // subscriber sees just EOF.
        Assert.Empty(payloads);
    }

    [Fact]
    public async Task Should_fan_out_snapshot_to_every_subscriber()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create();
        using var first = broadcaster.Subscribe();
        using var second = broadcaster.Subscribe();
        var snapshot = new BroadcasterTestPayload(1);

        // Act
        Assert.True(broadcaster.TryPublish(snapshot));
        broadcaster.Complete();

        var firstPayloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(first);
        var secondPayloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(second);

        // Assert — both subscribers see the same snapshot instance.
        Assert.Multiple(
            () => Assert.Same(snapshot, Assert.Single(firstPayloads)),
            () => Assert.Same(snapshot, Assert.Single(secondPayloads)));
    }
}
