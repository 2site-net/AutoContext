namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Protocol.Messages.Config;

public sealed class ConfigFrameStreamTests
{
    [Fact]
    public async Task Should_yield_snapshots_in_order_as_SnapshotFrames()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonConfigSnapshot>();
        var first = new JsonConfigSnapshot { Version = "first" };
        var second = new JsonConfigSnapshot { Version = "second" };

        Assert.True(channel.Writer.TryWrite(first));
        Assert.True(channel.Writer.TryWrite(second));
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonConfigSnapshot>(
            channel.Reader,
            release: () => { },
            wasDropped: () => false);

        // Act
        var frames = new List<JsonConfigStreamFrame>();
        await foreach (var frame in new ConfigFrameStream().StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert — two snapshot frames in FIFO order, no terminal.
        Assert.Multiple(
            () => Assert.Equal(2, frames.Count),
            () => Assert.Same(first, Assert.IsType<JsonConfigSnapshotFrame>(frames[0]).Snapshot),
            () => Assert.Same(second, Assert.IsType<JsonConfigSnapshotFrame>(frames[1]).Snapshot));
    }

    [Fact]
    public async Task Should_not_yield_terminal_frame_when_not_dropped()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonConfigSnapshot>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonConfigSnapshot>(
            channel.Reader,
            release: () => { },
            wasDropped: () => false);

        // Act
        var frames = new List<JsonConfigStreamFrame>();
        await foreach (var frame in new ConfigFrameStream().StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert
        Assert.Empty(frames);
    }

    [Fact]
    public async Task Should_yield_terminal_dropped_frame_when_wasDropped_is_true()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<JsonConfigSnapshot>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonConfigSnapshot>(
            channel.Reader,
            release: () => { },
            wasDropped: () => true);

        // Act
        var frames = new List<JsonConfigStreamFrame>();
        await foreach (var frame in new ConfigFrameStream().StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert
        var terminal = Assert.IsType<JsonConfigDroppedFrame>(Assert.Single(frames));
        Assert.Equal(JsonConfigDroppedFrame.SlowSubscriberReason, terminal.Reason);
    }
}
