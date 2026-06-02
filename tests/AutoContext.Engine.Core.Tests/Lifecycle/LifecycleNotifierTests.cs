namespace AutoContext.Engine.Core.Tests.Lifecycle;

using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Options;

public sealed class LifecycleNotifierTests
{
    [Fact]
    public async Task Should_publish_shutting_down_event_with_owning_instance_id()
    {
        // Arrange
        var options = EngineOptionsFakeData.CreateValidOptions();
        var stream = LifecycleEventStreamFakeData.CreateStream(options);
        var sut = new LifecycleNotifier(stream, Options.Create(options));
        using var subscription = stream.Subscribe();

        // Act
        var notified = sut.NotifyShutdown();

        // Assert
        var events = await LifecycleStreamTestReader.ReadAllAsync(
            subscription, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.True(notified),
            () => Assert.Equal(2, events.Count),
            () => Assert.Equal(LifecycleEventKinds.Started, events[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, events[1].Kind),
            () => Assert.Equal(options.InstanceId, events[1].InstanceId),
            () => Assert.Equal(0L, events[1].Revision));
    }

    [Fact]
    public void Should_return_false_when_NotifyShutdown_is_invoked_twice()
    {
        // Arrange
        var options = EngineOptionsFakeData.CreateValidOptions();
        var sut = new LifecycleNotifier(
            LifecycleEventStreamFakeData.CreateStream(options),
            Options.Create(options));

        // Act + Assert
        Assert.Multiple(
            () => Assert.True(sut.NotifyShutdown()),
            () => Assert.False(sut.NotifyShutdown()));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_event_stream()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleNotifier(null!, Options.Create(EngineOptionsFakeData.CreateValidOptions())));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        var options = EngineOptionsFakeData.CreateValidOptions();

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleNotifier(LifecycleEventStreamFakeData.CreateStream(options), null!));
    }
}
