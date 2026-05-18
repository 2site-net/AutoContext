namespace AutoContext.Engine.Core.Tests.Lifecycle;

using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Testing.Utils;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class LifecycleEventStreamTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleEventStream(
                null!,
                NullLogger<LifecycleEventStream>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleEventStream(
                Options.Create(NewOptions()),
                null!));
    }

    [Fact]
    public async Task Should_seed_started_event_with_owning_instance_id_on_subscribe()
    {
        // Arrange
        var options = NewOptions();
        var sut = NewStream(options);

        // Act
        using var subscription = sut.Subscribe();
        var events = await ReadUntilCompletionAsync(subscription, expectedCount: 1);

        // Assert
        var started = Assert.Single(events);
        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.Started, started.Kind),
            () => Assert.Equal(options.InstanceId, started.InstanceId),
            () => Assert.Equal(0L, started.Revision));
    }

    [Fact]
    public async Task Should_fan_out_terminal_event_to_every_active_subscriber()
    {
        // Arrange
        var sut = NewStream(NewOptions());
        using var first = sut.Subscribe();
        using var second = sut.Subscribe();
        var terminal = new LifecycleEvent
        {
            Kind = LifecycleEventKinds.ShuttingDown,
            InstanceId = Guid.NewGuid(),
            Revision = 0,
        };

        // Act
        var completed = sut.TryComplete(terminal);

        // Assert — each subscriber observes started THEN the
        // terminal event, and the channel completes so the read
        // enumeration ends.
        Assert.True(completed);
        var firstEvents = await ReadUntilCompletionAsync(first, expectedCount: 2);
        var secondEvents = await ReadUntilCompletionAsync(second, expectedCount: 2);

        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.Started, firstEvents[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, firstEvents[1].Kind),
            () => Assert.Equal(LifecycleEventKinds.Started, secondEvents[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, secondEvents[1].Kind));
    }

    [Fact]
    public void Should_be_idempotent_when_TryComplete_is_invoked_twice()
    {
        // Arrange
        var sut = NewStream(NewOptions());
        var terminal = new LifecycleEvent
        {
            Kind = LifecycleEventKinds.ShuttingDown,
            InstanceId = Guid.NewGuid(),
        };

        // Act + Assert — first call returns true, second returns false.
        Assert.Multiple(
            () => Assert.True(sut.TryComplete(terminal)),
            () => Assert.False(sut.TryComplete(terminal)));
    }

    [Fact]
    public async Task Should_replay_terminal_event_to_late_subscribers()
    {
        // Arrange — a subscriber that joins AFTER the stream has
        // completed must still see (started, terminal) so it can
        // act on the current snapshot key one last time before the
        // connection closes.
        var sut = NewStream(NewOptions());
        var terminal = new LifecycleEvent
        {
            Kind = LifecycleEventKinds.ShuttingDown,
            InstanceId = Guid.NewGuid(),
        };
        sut.TryComplete(terminal);

        // Act
        using var subscription = sut.Subscribe();
        var events = await ReadUntilCompletionAsync(subscription, expectedCount: 2);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.Started, events[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, events[1].Kind));
    }

    [Fact]
    public async Task Should_unsubscribe_and_complete_channel_on_subscription_dispose()
    {
        // Arrange
        var sut = NewStream(NewOptions());
        var subscription = sut.Subscribe();

        // Act
        subscription.Dispose();

        // Assert — channel completes, enumeration ends after the
        // seeded started event is consumed.
        var events = await ReadUntilCompletionAsync(subscription, expectedCount: 1);
        Assert.Equal(LifecycleEventKinds.Started, events[0].Kind);
    }

    [Fact]
    public void Should_throw_when_TryPublish_is_invoked_with_null_event()
    {
        var sut = NewStream(NewOptions());
        Assert.Throws<ArgumentNullException>(() => sut.TryPublish(null!));
    }

    [Fact]
    public void Should_throw_when_TryComplete_is_invoked_with_null_event()
    {
        var sut = NewStream(NewOptions());
        Assert.Throws<ArgumentNullException>(() => sut.TryComplete(null!));
    }

    [Fact]
    public async Task Should_evict_subscriber_when_bounded_buffer_overflows()
    {
        // Arrange — never drain the subscriber. Publish more events
        // than the buffer can hold; the first batch fills the
        // buffer (including the seeded started event), the next
        // TryPublish trips the eviction path.
        var sut = NewStream(NewOptions());
        using var subscription = sut.Subscribe();
        var evt = new LifecycleEvent { Kind = LifecycleEventKinds.Reloading };

        // Act — fill the buffer (capacity 64) leaving exactly the
        // seeded started event plus 63 fillers, then publish one
        // more to overflow.
        for (var i = 0; i < LifecycleEventStream.SubscriberBufferCapacity; i++)
        {
            Assert.True(sut.TryPublish(evt));
        }

        // Assert — the reader, after draining everything, sees the
        // terminal evicted frame.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var events = new List<LifecycleEvent>();

        await foreach (var observed in subscription.ReadAllAsync(cts.Token))
        {
            events.Add(observed);
        }

        Assert.Contains(events, e => e.Kind == LifecycleEventKinds.Evicted);
    }

    [Fact]
    public void Should_return_false_from_TryPublish_after_stream_completes()
    {
        // Arrange
        var sut = NewStream(NewOptions());
        sut.TryComplete(new LifecycleEvent { Kind = LifecycleEventKinds.ShuttingDown });

        // Act + Assert
        Assert.False(sut.TryPublish(new LifecycleEvent
        {
            Kind = LifecycleEventKinds.Reloading,
        }));
    }

    private static EngineOptions NewOptions()
        => new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
        };

    private static LifecycleEventStream NewStream(EngineOptions options)
        => new(Options.Create(options), NullLogger<LifecycleEventStream>.Instance);

    private static async Task<List<LifecycleEvent>> ReadUntilCompletionAsync(
        LifecycleEventSubscription subscription,
        int expectedCount)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var events = new List<LifecycleEvent>();

        await foreach (var evt in subscription.ReadAllAsync(cts.Token))
        {
            events.Add(evt);

            if (events.Count >= expectedCount)
            {
                subscription.Dispose();
            }
        }

        return events;
    }
}
