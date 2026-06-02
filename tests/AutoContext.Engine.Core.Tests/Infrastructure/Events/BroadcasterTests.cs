namespace AutoContext.Engine.Core.Tests.Infrastructure.Events;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class BroadcasterTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new Broadcaster<BroadcasterTestPayload>(null!, "test-channel"));
    }

    [Fact]
    public void Should_throw_when_constructed_with_empty_channel()
    {
        // Act + Assert
        Assert.Throws<ArgumentException>(
            () => new Broadcaster<BroadcasterTestPayload>(
                NullLogger<Broadcaster<BroadcasterTestPayload>>.Instance, ""));
    }

    [Fact]
    public void Should_throw_when_publishing_null_payload()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => broadcaster.TryPublish(null!));
    }

    [Fact]
    public void Should_be_idempotent_when_complete_is_called_twice()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();

        // Act + Assert — second call must not throw.
        broadcaster.Complete();
        broadcaster.Complete();
    }

    [Fact]
    public void Should_return_false_when_publishing_after_complete()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();
        broadcaster.Complete();

        // Act
        var accepted = broadcaster.TryPublish(new BroadcasterTestPayload(1));

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task Should_return_empty_subscription_when_subscribing_after_complete()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();
        broadcaster.Complete();

        // Act
        using var subscription = broadcaster.Subscribe();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — no payloads, and the subscriber was not evicted
        // (graceful completion is not an eviction).
        Assert.Multiple(
            () => Assert.Empty(payloads),
            () => Assert.False(subscription.WasEvicted));
    }

    [Fact]
    public async Task Should_seed_payloads_ahead_of_live_tail()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();
        var seed = new BroadcasterTestPayload(1);

        // Act
        using var subscription = broadcaster.Subscribe(seed);
        broadcaster.Complete();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the seed lands as the first (and only) payload.
        Assert.Same(seed, Assert.Single(payloads));
    }

    [Fact]
    public async Task Should_fan_out_payload_to_every_subscriber()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();
        using var first = broadcaster.Subscribe();
        using var second = broadcaster.Subscribe();
        var payload = new BroadcasterTestPayload(1);

        // Act
        Assert.True(broadcaster.TryPublish(payload));
        broadcaster.Complete();

        var firstPayloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(first);
        var secondPayloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(second);

        // Assert — both subscribers see the same payload instance.
        Assert.Multiple(
            () => Assert.Same(payload, Assert.Single(firstPayloads)),
            () => Assert.Same(payload, Assert.Single(secondPayloads)));
    }

    [Fact]
    public async Task Should_complete_active_subscription_on_complete()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();
        using var subscription = broadcaster.Subscribe();
        var payload = new BroadcasterTestPayload(1);
        Assert.True(broadcaster.TryPublish(payload));

        // Act
        broadcaster.Complete();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        // Assert — the payload plus EOF, no eviction.
        Assert.Multiple(
            () => Assert.Same(payload, Assert.Single(payloads)),
            () => Assert.False(subscription.WasEvicted));
    }

    [Fact]
    public async Task Should_evict_slow_subscriber_on_overflow()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();
        using var slow = broadcaster.Subscribe();

        // Act — publish capacity + 1 payloads without draining. The
        // (capacity+1)-th publish triggers eviction.
        for (var i = 0; i <= Broadcaster<BroadcasterTestPayload>.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(new BroadcasterTestPayload(i)));
        }

        broadcaster.Complete();
        var payloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(slow);

        // Assert — buffered payloads up to capacity, and the
        // subscriber was evicted for slowness.
        Assert.Multiple(
            () => Assert.Equal(Broadcaster<BroadcasterTestPayload>.SubscriberBufferCapacity, payloads.Count),
            () => Assert.True(slow.WasEvicted));
    }

    [Fact]
    public async Task Should_keep_survivor_flowing_when_sibling_is_evicted()
    {
        // Arrange
        var broadcaster = BroadcasterTestFactory.Create();
        using var slow = broadcaster.Subscribe();
        using var fast = broadcaster.Subscribe();

        await using var fastEnumerator = fast
            .ReadAllAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        // Act — interleave publish + fast-side drain so 'fast' never
        // fills, while 'slow' is starved and overflows.
        var payload = new BroadcasterTestPayload(1);
        for (var i = 0; i <= Broadcaster<BroadcasterTestPayload>.SubscriberBufferCapacity; i++)
        {
            Assert.True(broadcaster.TryPublish(payload));
            Assert.True(await fastEnumerator.MoveNextAsync());
        }

        broadcaster.Complete();
        var slowPayloads = await BroadcasterSubscriptionTestDrainer.DrainAsync(slow);

        // Assert — slow was evicted; fast kept pace and was not.
        Assert.Multiple(
            () => Assert.Equal(Broadcaster<BroadcasterTestPayload>.SubscriberBufferCapacity, slowPayloads.Count),
            () => Assert.True(slow.WasEvicted),
            () => Assert.False(fast.WasEvicted));
    }
}
