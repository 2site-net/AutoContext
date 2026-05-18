namespace AutoContext.Engine.Core.Tests.Lifecycle;

using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Testing.Utils;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class LifecycleNotifierTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_event_stream()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleNotifier(null!, Options.Create(NewOptions())));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleNotifier(NewStream(NewOptions()), null!));
    }

    [Fact]
    public async Task Should_publish_shutting_down_event_with_owning_instance_id()
    {
        // Arrange
        var options = NewOptions();
        var stream = NewStream(options);
        var sut = new LifecycleNotifier(stream, Options.Create(options));
        using var subscription = stream.Subscribe();

        // Act
        var notified = sut.NotifyShutdown();

        // Assert — subscriber observes started THEN shutting-down,
        // and the stream completes.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var events = new List<LifecycleEvent>();

        await foreach (var evt in subscription.ReadAllAsync(cts.Token))
        {
            events.Add(evt);
        }

        Assert.True(notified);
        Assert.Multiple(
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
        var options = NewOptions();
        var sut = new LifecycleNotifier(NewStream(options), Options.Create(options));

        // Act + Assert
        Assert.Multiple(
            () => Assert.True(sut.NotifyShutdown()),
            () => Assert.False(sut.NotifyShutdown()));
    }

    private static EngineOptions NewOptions()
        => new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
        };

    private static LifecycleEventStream NewStream(EngineOptions options)
        => new(Options.Create(options), NullLogger<LifecycleEventStream>.Instance);
}
