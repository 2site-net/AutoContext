namespace AutoContext.Engine.Core.Tests.Lifecycle;

using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class LifecycleEventStreamTests
{
    [Fact]
    public void Should_be_idempotent_when_TryComplete_is_invoked_twice()
    {
        // Arrange
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());
        var terminal = LifecycleEventStreamFakeData.CreateTerminalEvent();

        // Act + Assert
        Assert.Multiple(
            () => Assert.True(sut.TryComplete(terminal)),
            () => Assert.False(sut.TryComplete(terminal)));
    }

    [Fact]
    public async Task Should_evict_subscriber_when_bounded_buffer_overflows()
    {
        // Arrange
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());
        using var subscription = sut.Subscribe();

        // Act
        OverflowSubscriberBuffer();

        // Assert
        var events = await LifecycleSubscriptionTestReader.ReadAllAsync(
            subscription, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(events, e => e.Kind == LifecycleEventKinds.Evicted);

        void OverflowSubscriberBuffer()
        {
            var evt = new JsonLifecycleEvent { Kind = LifecycleEventKinds.Reloading };

            for (var i = 0; i < LifecycleEventStream.SubscriberBufferCapacity + 1; i++)
            {
                sut.TryPublish(evt);
            }
        }
    }

    [Fact]
    public async Task Should_fan_out_terminal_event_to_every_active_subscriber()
    {
        // Arrange
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());
        using var first = sut.Subscribe();
        using var second = sut.Subscribe();
        var terminal = LifecycleEventStreamFakeData.CreateTerminalEvent();

        // Act
        var completed = sut.TryComplete(terminal);

        // Assert
        var firstEvents = await LifecycleSubscriptionTestReader.ReadUntilCountAsync(
            first, expectedCount: 2, cancellationToken: TestContext.Current.CancellationToken);
        var secondEvents = await LifecycleSubscriptionTestReader.ReadUntilCountAsync(
            second, expectedCount: 2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.True(completed),
            () => Assert.Equal(LifecycleEventKinds.Started, firstEvents[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, firstEvents[1].Kind),
            () => Assert.Equal(LifecycleEventKinds.Started, secondEvents[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, secondEvents[1].Kind));
    }

    [Fact]
    public async Task Should_replay_terminal_event_to_late_subscribers()
    {
        // Arrange
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());
        sut.TryComplete(LifecycleEventStreamFakeData.CreateTerminalEvent());

        // Act
        using var subscription = sut.Subscribe();

        // Assert
        var events = await LifecycleSubscriptionTestReader.ReadAllAsync(
            subscription, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(2, events.Count),
            () => Assert.Equal(LifecycleEventKinds.Started, events[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, events[1].Kind));
    }

    [Fact]
    public void Should_return_false_from_TryPublish_after_stream_completes()
    {
        // Arrange
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());
        sut.TryComplete(LifecycleEventStreamFakeData.CreateTerminalEvent());

        // Act
        var published = sut.TryPublish(new JsonLifecycleEvent { Kind = LifecycleEventKinds.Reloading });

        // Assert
        Assert.False(published);
    }

    [Fact]
    public async Task Should_seed_started_event_with_owning_instance_id_on_subscribe()
    {
        // Arrange
        var options = EngineOptionsFakeData.CreateValidOptions();
        var sut = LifecycleEventStreamFakeData.CreateStream(options);

        // Act
        using var subscription = sut.Subscribe();

        // Assert
        var events = await LifecycleSubscriptionTestReader.ReadUntilCountAsync(
            subscription, expectedCount: 1, cancellationToken: TestContext.Current.CancellationToken);
        var started = Assert.Single(events);

        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.Started, started.Kind),
            () => Assert.Equal(options.InstanceId, started.InstanceId),
            () => Assert.Equal(0L, started.Revision));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleEventStream(Options.Create(EngineOptionsFakeData.CreateValidOptions()), null!));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleEventStream(null!, NullLogger<LifecycleEventStream>.Instance));
    }

    [Fact]
    public void Should_throw_when_TryComplete_is_invoked_with_null_event()
    {
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());

        Assert.Throws<ArgumentNullException>(() => sut.TryComplete(null!));
    }

    [Fact]
    public void Should_throw_when_TryPublish_is_invoked_with_null_event()
    {
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());

        Assert.Throws<ArgumentNullException>(() => sut.TryPublish(null!));
    }

    [Fact]
    public async Task Should_unsubscribe_and_complete_channel_on_subscription_dispose()
    {
        // Arrange
        var sut = LifecycleEventStreamFakeData.CreateStream(EngineOptionsFakeData.CreateValidOptions());
        var subscription = sut.Subscribe();

        // Act
        subscription.Dispose();

        // Assert
        var events = await LifecycleSubscriptionTestReader.ReadAllAsync(
            subscription, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Single(events),
            () => Assert.Equal(LifecycleEventKinds.Started, events[0].Kind));
    }
}
