namespace AutoContext.Client.Core.Engine.Subscriptions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Server-streaming consumer of the engine's <c>Logs.TailEngine</c>
/// channel — a pure live tail of the engine's <see cref="JsonLogRecord"/>
/// firehose. Each enumeration opens its own dedicated <c>rpc</c>
/// connection, yields each record as it is drained, and disposes the
/// connection when the caller stops enumerating. A slow-subscriber drop
/// surfaces as an <see cref="EngineSubscriptionDroppedException"/>.
/// </summary>
public sealed class LogsTailSubscription
{
    private readonly EngineConnector _connector;

    /// <summary>
    /// Creates a new <see cref="LogsTailSubscription"/> that resolves an
    /// engine through <paramref name="connector"/> on each subscribe.
    /// </summary>
    /// <param name="connector">Find-or-spawn resolver. Must not be
    /// <see langword="null"/>.</param>
    public LogsTailSubscription(EngineConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);

        _connector = connector;
    }

    /// <summary>
    /// Tails the engine log, yielding each record until the caller stops
    /// enumerating or the engine completes the stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the stream.</param>
    /// <exception cref="EngineSubscriptionDroppedException">The engine
    /// dropped this subscription because the consumer fell behind.</exception>
    public async IAsyncEnumerable<JsonLogRecord> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = await _connector
            .ConnectAsync(EndpointKind.Rpc, cancellationToken)
            .ConfigureAwait(false);

        await using (connection.ConfigureAwait(false))
        {
            var frames = connection.SubscribeAsync(
                LogsMethods.TailEngine, parameters: null, cancellationToken);

            await foreach (var element in frames.ConfigureAwait(false))
            {
                var frame = element.Deserialize(ProtocolJsonContext.Default.JsonLogStreamFrame);
                if (frame is JsonLogRecordFrame record)
                {
                    yield return record.Record;
                    continue;
                }

                if (frame is JsonLogDroppedFrame dropped)
                {
                    throw new EngineSubscriptionDroppedException(LogsMethods.TailEngine, dropped.Reason);
                }

                yield break;
            }
        }
    }
}
