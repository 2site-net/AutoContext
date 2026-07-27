namespace AutoContext.Client.Core.Engine.Subscriptions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Agent;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Server-streaming consumer of the engine's
/// <c>Agent.Events.Subscribe</c> channel — a pure live tail of the
/// agent-loop events the engine re-broadcasts, with no
/// snapshot-on-subscribe seed. Each enumeration opens its own dedicated
/// <c>rpc</c> connection, yields each <see cref="JsonAgentEvent"/> as it
/// arrives, and disposes the connection when the caller stops
/// enumerating. A slow-subscriber drop surfaces as an
/// <see cref="EngineSubscriptionDroppedException"/>.
/// </summary>
public sealed class AgentEventsSubscription
{
    private readonly EngineConnector _connector;

    /// <summary>
    /// Creates a new <see cref="AgentEventsSubscription"/> that resolves
    /// an engine through <paramref name="connector"/> on each subscribe.
    /// </summary>
    /// <param name="connector">Find-or-spawn resolver. Must not be
    /// <see langword="null"/>.</param>
    public AgentEventsSubscription(EngineConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);

        _connector = connector;
    }

    /// <summary>
    /// Subscribes to agent events, yielding each event until the caller
    /// stops enumerating or the engine completes the stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the stream.</param>
    /// <exception cref="EngineSubscriptionDroppedException">The engine
    /// dropped this subscription because the consumer fell behind.</exception>
    public async IAsyncEnumerable<JsonAgentEvent> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = await _connector
            .ConnectAsync(EndpointKind.Rpc, cancellationToken)
            .ConfigureAwait(false);

        await using (connection.ConfigureAwait(false))
        {
            var frames = connection.SubscribeAsync(
                AgentMethods.EventsSubscribe, parameters: null, cancellationToken);

            await foreach (var element in frames.ConfigureAwait(false))
            {
                var agentEvent = element.Deserialize(ProtocolJsonContext.Default.JsonAgentEvent);
                if (agentEvent is null)
                {
                    yield break;
                }

                if (string.Equals(agentEvent.Kind, AgentEventKinds.Dropped, StringComparison.Ordinal))
                {
                    throw new EngineSubscriptionDroppedException(
                        AgentMethods.EventsSubscribe, agentEvent.Reason ?? AgentEventKinds.Dropped);
                }

                yield return agentEvent;
            }
        }
    }
}
