namespace AutoContext.Engine.Core.Tests.Infrastructure.Events;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Logs;

public sealed class SubscriberTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new Subscriber<JsonLogRecord>(null!));
    }

    [Fact]
    public void Should_start_in_active_state()
    {
        // Arrange
        var subscriber = SubscriberTestFactory.Create();

        // Assert
        Assert.False(subscriber.WasEvicted);
    }

    [Fact]
    public void Should_transition_to_closed_via_TryClose()
    {
        // Arrange
        var subscriber = SubscriberTestFactory.Create();

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
        var subscriber = SubscriberTestFactory.Create();

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
        var subscriber = SubscriberTestFactory.Create();
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
        var subscriber = SubscriberTestFactory.Create();
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
        var subscriber = SubscriberTestFactory.Create();
        Assert.True(subscriber.TryClose());

        // Act
        var secondCall = subscriber.TryClose();

        // Assert
        Assert.False(secondCall);
    }
}
