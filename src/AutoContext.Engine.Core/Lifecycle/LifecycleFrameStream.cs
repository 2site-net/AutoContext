namespace AutoContext.Engine.Core.Lifecycle;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of
/// <see cref="JsonLifecycleEvent"/> onto the lifecycle wire stream. Each
/// event passes through unchanged; a slow-subscriber drop appends a
/// terminal <see cref="LifecycleEventKinds.Dropped"/> event.
/// </summary>
/// <remarks>
/// <see cref="ToFrame"/> is the identity mapping because the lifecycle wire
/// shape is a flat kind-tagged record — <see cref="JsonLifecycleEvent.Kind"/>
/// is a field, so the payload already <em>is</em> its own frame. This differs
/// from the log and config streams, whose discriminated-envelope frames
/// (<c>*StreamFrame</c>) carry a distinct subtype per kind and therefore need a
/// real payload-to-frame wrap.
/// </remarks>
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
