namespace AutoContext.Engine.Core.Lifecycle;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of
/// <see cref="JsonLifecycleEvent"/> onto the lifecycle wire stream. Each
/// event passes through unchanged; a slow-subscriber drop appends a
/// terminal <see cref="LifecycleEventKinds.Dropped"/> event.
/// </summary>
internal sealed class LifecycleFrameStream : BroadcasterFrameStream<JsonLifecycleEvent, JsonLifecycleEvent>
{
    /// <inheritdoc/>
    protected override JsonLifecycleEvent CreateDroppedFrame()
        => new()
        {
            Kind = LifecycleEventKinds.Dropped,
            Reason = "slow-subscriber",
        };

    /// <inheritdoc/>
    protected override JsonLifecycleEvent ToFrame(JsonLifecycleEvent payload)
        => payload;
}
