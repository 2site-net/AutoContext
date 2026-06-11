namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using System.Threading.Channels;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Instructions;

public sealed class InstructionsFrameStreamTests
{
    [Fact]
    public async Task Should_yield_snapshots_in_order_as_SnapshotFrames()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<IReadOnlyList<JsonInstructionsListRow>>();
        IReadOnlyList<JsonInstructionsListRow> first = [new JsonInstructionsListRow { Key = "first" }];
        IReadOnlyList<JsonInstructionsListRow> second = [new JsonInstructionsListRow { Key = "second" }];

        Assert.True(channel.Writer.TryWrite(first));
        Assert.True(channel.Writer.TryWrite(second));
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<IReadOnlyList<JsonInstructionsListRow>>(
            channel.Reader,
            release: () => { },
            wasDropped: () => false);

        // Act
        var frames = new List<JsonInstructionsStreamFrame>();
        await foreach (var frame in new InstructionsFrameStream().StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert — two snapshot frames in FIFO order, no terminal.
        Assert.Multiple(
            () => Assert.Equal(2, frames.Count),
            () => Assert.Same(first, Assert.IsType<JsonInstructionsSnapshotFrame>(frames[0]).Files),
            () => Assert.Same(second, Assert.IsType<JsonInstructionsSnapshotFrame>(frames[1]).Files));
    }

    [Fact]
    public async Task Should_not_yield_terminal_frame_when_not_dropped()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<IReadOnlyList<JsonInstructionsListRow>>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<IReadOnlyList<JsonInstructionsListRow>>(
            channel.Reader,
            release: () => { },
            wasDropped: () => false);

        // Act
        var frames = new List<JsonInstructionsStreamFrame>();
        await foreach (var frame in new InstructionsFrameStream().StreamAsync(subscription, TestContext.Current.CancellationToken))
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
        var channel = Channel.CreateUnbounded<IReadOnlyList<JsonInstructionsListRow>>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<IReadOnlyList<JsonInstructionsListRow>>(
            channel.Reader,
            release: () => { },
            wasDropped: () => true);

        // Act
        var frames = new List<JsonInstructionsStreamFrame>();
        await foreach (var frame in new InstructionsFrameStream().StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert
        var terminal = Assert.IsType<JsonInstructionsDroppedFrame>(Assert.Single(frames));
        Assert.Equal(JsonInstructionsDroppedFrame.SlowSubscriberReason, terminal.Reason);
    }
}
