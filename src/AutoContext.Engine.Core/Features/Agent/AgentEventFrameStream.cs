namespace AutoContext.Engine.Core.Features.Agent;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Agent;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of
/// <see cref="JsonAgentEvent"/> onto the <c>Agent.Events.Subscribe</c>
/// wire stream. Each event passes through unchanged; a slow-subscriber
/// drop appends a terminal <see cref="AgentEventKinds.Dropped"/> event.
/// </summary>
/// <remarks>
/// <see cref="ToFrame"/> is the identity mapping because the agent wire
/// shape is a flat kind-tagged record — <see cref="JsonAgentEvent.Kind"/>
/// is a field, so the payload already <em>is</em> its own frame, the same
/// shape the lifecycle stream uses.
/// </remarks>
internal sealed class AgentEventFrameStream : BroadcasterFrameStream<JsonAgentEvent, JsonAgentEvent>
{
    /// <inheritdoc/>
    protected override JsonAgentEvent CreateDroppedFrame()
        => new()
        {
            Kind = AgentEventKinds.Dropped,
            Reason = "slow-subscriber",
        };

    /// <inheritdoc/>
    protected override JsonAgentEvent ToFrame(JsonAgentEvent payload)
        => payload;
}
