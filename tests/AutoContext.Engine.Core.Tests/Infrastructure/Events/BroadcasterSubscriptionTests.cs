namespace AutoContext.Engine.Core.Tests.Infrastructure.Events;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

public sealed class BroadcasterSubscriptionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_reader()
    {
        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            new BroadcasterSubscription<BroadcasterTestPayload>(null!, release: () => { }, wasDropped: () => false));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_release()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<BroadcasterTestPayload>();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            new BroadcasterSubscription<BroadcasterTestPayload>(channel.Reader, release: null!, wasDropped: () => false));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_wasDropped()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<BroadcasterTestPayload>();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() =>
            new BroadcasterSubscription<BroadcasterTestPayload>(channel.Reader, release: () => { }, wasDropped: null!));
    }

    [Fact]
    public void Should_invoke_release_callback_on_dispose()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<BroadcasterTestPayload>();
        var releaseCount = 0;
        var subscription = new BroadcasterSubscription<BroadcasterTestPayload>(
            channel.Reader,
            release: () => Interlocked.Increment(ref releaseCount),
            wasDropped: () => false);

        // Act
        subscription.Dispose();

        // Assert
        Assert.Equal(1, releaseCount);
    }

    [Fact]
    public void Should_invoke_release_callback_exactly_once_when_disposed_twice()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<BroadcasterTestPayload>();
        var releaseCount = 0;
        var subscription = new BroadcasterSubscription<BroadcasterTestPayload>(
            channel.Reader,
            release: () => Interlocked.Increment(ref releaseCount),
            wasDropped: () => false);

        // Act
        subscription.Dispose();
        subscription.Dispose();

        // Assert
        Assert.Equal(1, releaseCount);
    }
}
