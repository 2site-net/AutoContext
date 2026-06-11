namespace AutoContext.Engine.Core.Features.Instructions;

using System.Collections.Generic;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Instructions;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of corpus listing
/// snapshots onto the <see cref="JsonInstructionsStreamFrame"/> wire
/// protocol: each listing becomes a
/// <see cref="JsonInstructionsSnapshotFrame"/> and a slow-subscriber
/// drop appends a terminal <see cref="JsonInstructionsDroppedFrame"/>.
/// </summary>
internal sealed class InstructionsFrameStream
    : BroadcasterFrameStream<IReadOnlyList<JsonInstructionsListRow>, JsonInstructionsStreamFrame>
{
    /// <inheritdoc/>
    protected override JsonInstructionsStreamFrame CreateDroppedFrame()
        => new JsonInstructionsDroppedFrame(JsonInstructionsDroppedFrame.SlowSubscriberReason);

    /// <inheritdoc/>
    protected override JsonInstructionsStreamFrame ToFrame(IReadOnlyList<JsonInstructionsListRow> payload)
        => new JsonInstructionsSnapshotFrame(payload);
}
