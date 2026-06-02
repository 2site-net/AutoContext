namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Config;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of
/// <see cref="JsonConfigSnapshot"/> onto the
/// <see cref="JsonConfigStreamFrame"/> wire protocol: each snapshot
/// becomes a <see cref="JsonConfigSnapshotFrame"/> and a slow-subscriber
/// drop appends a terminal <see cref="JsonConfigDroppedFrame"/>.
/// </summary>
internal sealed class ConfigFrameStream : BroadcasterFrameStream<JsonConfigSnapshot, JsonConfigStreamFrame>
{
    /// <inheritdoc/>
    protected override JsonConfigStreamFrame CreateDroppedFrame()
        => new JsonConfigDroppedFrame(JsonConfigDroppedFrame.SlowSubscriberReason);

    /// <inheritdoc/>
    protected override JsonConfigStreamFrame ToFrame(JsonConfigSnapshot payload)
        => new JsonConfigSnapshotFrame(payload);
}
