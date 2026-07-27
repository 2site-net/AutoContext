namespace AutoContext.Client.Core.Engine.Subscriptions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Server-streaming consumer of the engine's <c>Config.Subscribe</c>
/// channel. Each enumeration opens its own dedicated <c>rpc</c>
/// connection (a subscription monopolises a connection's read side),
/// yields the snapshot-on-subscribe seed followed by a fresh snapshot
/// on every config change, and disposes the connection when the caller
/// stops enumerating. A slow-subscriber drop surfaces as an
/// <see cref="EngineSubscriptionDroppedException"/>.
/// </summary>
public sealed class ConfigSubscription
{
    private readonly EngineConnector _connector;

    /// <summary>
    /// Creates a new <see cref="ConfigSubscription"/> that resolves an
    /// engine through <paramref name="connector"/> on each subscribe.
    /// </summary>
    /// <param name="connector">Find-or-spawn resolver. Must not be
    /// <see langword="null"/>.</param>
    public ConfigSubscription(EngineConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);

        _connector = connector;
    }

    /// <summary>
    /// Subscribes to config changes, yielding the current snapshot
    /// first and then one snapshot per change until the caller stops
    /// enumerating or the engine completes the stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the stream.</param>
    /// <exception cref="EngineSubscriptionDroppedException">The engine
    /// dropped this subscription because the consumer fell behind.</exception>
    public async IAsyncEnumerable<JsonConfigSnapshot> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = await _connector
            .ConnectAsync(EndpointKind.Rpc, cancellationToken)
            .ConfigureAwait(false);

        await using (connection.ConfigureAwait(false))
        {
            var frames = connection.SubscribeAsync(
                ConfigMethods.Subscribe, parameters: null, cancellationToken);

            await foreach (var element in frames.ConfigureAwait(false))
            {
                var frame = element.Deserialize(ProtocolJsonContext.Default.JsonConfigStreamFrame);
                if (frame is JsonConfigSnapshotFrame snapshot)
                {
                    yield return snapshot.Snapshot;
                    continue;
                }

                if (frame is JsonConfigDroppedFrame dropped)
                {
                    throw new EngineSubscriptionDroppedException(ConfigMethods.Subscribe, dropped.Reason);
                }

                yield break;
            }
        }
    }
}
