namespace AutoContext.Engine.Core.Tests.Infrastructure.Events;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

public sealed class BroadcasterSubscriberTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new BroadcasterSubscriber<BroadcasterTestPayload>(null!));
    }

    [Fact]
    public void Should_start_in_active_state()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();

        // Assert
        Assert.False(subscriber.WasDropped);
    }

    [Fact]
    public void Should_transition_to_closed_via_TryClose()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();

        // Act
        var closed = subscriber.TryClose();

        // Assert
        Assert.Multiple(
            () => Assert.True(closed),
            () => Assert.False(subscriber.WasDropped));
    }

    [Fact]
    public void Should_transition_to_dropped_via_TryDrop()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();

        // Act
        var dropped = subscriber.TryDrop();

        // Assert
        Assert.Multiple(
            () => Assert.True(dropped),
            () => Assert.True(subscriber.WasDropped));
    }

    [Fact]
    public void Should_refuse_TryClose_after_TryDrop()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();
        Assert.True(subscriber.TryDrop());

        // Act
        var closed = subscriber.TryClose();

        // Assert — state remains Dropped.
        Assert.Multiple(
            () => Assert.False(closed),
            () => Assert.True(subscriber.WasDropped));
    }

    [Fact]
    public void Should_refuse_TryDrop_after_TryClose()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();
        Assert.True(subscriber.TryClose());

        // Act
        var dropped = subscriber.TryDrop();

        // Assert — state remains Closed, WasDropped false.
        Assert.Multiple(
            () => Assert.False(dropped),
            () => Assert.False(subscriber.WasDropped));
    }

    [Fact]
    public void Should_be_idempotent_when_TryClose_is_called_twice()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();
        Assert.True(subscriber.TryClose());

        // Act
        var secondCall = subscriber.TryClose();

        // Assert
        Assert.False(secondCall);
    }
}
