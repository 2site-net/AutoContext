namespace AutoContext.Client.Core.Engine.Subscriptions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Server-streaming consumer of the engine's
/// <c>Instructions.Subscribe</c> channel. Each enumeration opens its
/// own dedicated <c>rpc</c> connection, yields the current listing as
/// the snapshot-on-subscribe seed followed by a fresh listing on every
/// corpus reload, and disposes the connection when the caller stops
/// enumerating. A slow-subscriber drop surfaces as an
/// <see cref="EngineSubscriptionDroppedException"/>.
/// </summary>
public sealed class InstructionsSubscription
{
    private readonly EngineConnector _connector;

    /// <summary>
    /// Creates a new <see cref="InstructionsSubscription"/> that
    /// resolves an engine through <paramref name="connector"/> on each
    /// subscribe.
    /// </summary>
    /// <param name="connector">Find-or-spawn resolver. Must not be
    /// <see langword="null"/>.</param>
    public InstructionsSubscription(EngineConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);

        _connector = connector;
    }

    /// <summary>
    /// Subscribes to corpus changes, yielding the current listing first
    /// and then one listing per reload until the caller stops
    /// enumerating or the engine completes the stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the stream.</param>
    /// <exception cref="EngineSubscriptionDroppedException">The engine
    /// dropped this subscription because the consumer fell behind.</exception>
    public async IAsyncEnumerable<IReadOnlyList<JsonInstructionsListRow>> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = await _connector
            .ConnectAsync(EndpointKind.Rpc, cancellationToken)
            .ConfigureAwait(false);

        await using (connection.ConfigureAwait(false))
        {
            var frames = connection.SubscribeAsync(
                InstructionsMethods.Subscribe, parameters: null, cancellationToken);

            await foreach (var element in frames.ConfigureAwait(false))
            {
                var frame = element.Deserialize(ProtocolJsonContext.Default.JsonInstructionsStreamFrame);
                if (frame is JsonInstructionsSnapshotFrame snapshot)
                {
                    yield return snapshot.Files;
                    continue;
                }

                if (frame is JsonInstructionsDroppedFrame dropped)
                {
                    throw new EngineSubscriptionDroppedException(
                        InstructionsMethods.Subscribe, dropped.Reason);
                }

                yield break;
            }
        }
    }
}
