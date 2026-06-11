namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Instructions;

[Trait("Category", "Integration")]
public sealed class InstructionsStreamTests
{
    [Fact]
    public async Task Should_replay_primed_snapshot_as_first_frame()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create<IReadOnlyList<JsonInstructionsListRow>>("Instructions.Subscribe");
        IReadOnlyList<JsonInstructionsListRow> primed = [new JsonInstructionsListRow { Key = "seed" }];
        broadcaster.Prime(primed);

        // Act — a new subscriber is seeded with the cached snapshot
        // through the real framer.
        using var subscription = broadcaster.Subscribe();
        broadcaster.Complete();
        var frames = await InstructionsStreamTestDrainer.DrainAsync(subscription);

        // Assert — the primed snapshot is replayed as the first
        // (and only) frame, with no terminal dropped frame.
        Assert.Same(primed, Assert.IsType<JsonInstructionsSnapshotFrame>(Assert.Single(frames)).Files);
    }

    [Fact]
    public async Task Should_yield_buffered_snapshots_then_terminal_dropped_frame_on_overflow()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create<IReadOnlyList<JsonInstructionsListRow>>("Instructions.Subscribe");
        using var slow = broadcaster.Subscribe();

        // Act — overflow a real snapshot broadcaster so it drops the
        // slow subscriber, then drain it through the real framer.
        for (var i = 0; i <= Broadcaster<IReadOnlyList<JsonInstructionsListRow>>.SubscriberBufferCapacity; i++)
        {
            IReadOnlyList<JsonInstructionsListRow> snapshot = [new JsonInstructionsListRow { Key = $"v{i}" }];
            Assert.True(broadcaster.TryPublish(snapshot));
        }

        broadcaster.Complete();
        var frames = await InstructionsStreamTestDrainer.DrainAsync(slow);

        // Assert — buffered snapshot frames up to capacity, then the
        // terminal dropped frame as the very last frame.
        var terminal = Assert.IsType<JsonInstructionsDroppedFrame>(frames[^1]);
        Assert.Multiple(
            () => Assert.Equal(JsonInstructionsDroppedFrame.SlowSubscriberReason, terminal.Reason),
            () => Assert.Equal(Broadcaster<IReadOnlyList<JsonInstructionsListRow>>.SubscriberBufferCapacity, frames.Count - 1),
            () => Assert.All(frames.Take(frames.Count - 1), frame => Assert.IsType<JsonInstructionsSnapshotFrame>(frame)));
    }
}
