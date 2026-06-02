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
        Assert.False(subscriber.WasEvicted);
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
            () => Assert.False(subscriber.WasEvicted));
    }

    [Fact]
    public void Should_transition_to_evicted_via_TryEvict()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();

        // Act
        var evicted = subscriber.TryEvict();

        // Assert
        Assert.Multiple(
            () => Assert.True(evicted),
            () => Assert.True(subscriber.WasEvicted));
    }

    [Fact]
    public void Should_refuse_TryClose_after_TryEvict()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();
        Assert.True(subscriber.TryEvict());

        // Act
        var closed = subscriber.TryClose();

        // Assert — state remains Evicted.
        Assert.Multiple(
            () => Assert.False(closed),
            () => Assert.True(subscriber.WasEvicted));
    }

    [Fact]
    public void Should_refuse_TryEvict_after_TryClose()
    {
        // Arrange
        var subscriber = BroadcasterSubscriberTestFactory.Create();
        Assert.True(subscriber.TryClose());

        // Act
        var evicted = subscriber.TryEvict();

        // Assert — state remains Closed, WasEvicted false.
        Assert.Multiple(
            () => Assert.False(evicted),
            () => Assert.False(subscriber.WasEvicted));
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
