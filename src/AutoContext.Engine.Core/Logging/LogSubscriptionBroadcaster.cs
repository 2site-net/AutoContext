namespace AutoContext.Engine.Core.Logging;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging;

/// <summary>
/// Singleton fan-out broadcaster backing the engine's
/// <c>logs</c> named pipe (and the <c>Logs.TailEngine</c> RPC
/// stream, when wired): every connection that opens the
/// <c>logs</c> pipe calls
/// <see cref="SubscriptionBroadcaster{TPayload, TSubscription}.Subscribe"/>
/// to receive a per-subscriber bounded buffer of
/// <see cref="JsonLogRecord"/> values drained by
/// <see cref="LogFileSinkService"/>.
/// </summary>
/// <remarks>
/// Each subscriber owns its own bounded <see cref="Channel{T}"/>.
/// A slow subscriber is evicted with a terminal
/// <see cref="JsonLogEvictedFrame"/> while the remaining subscribers
/// keep flowing. The file sink stays unaffected by subscriber
/// slowness — it is a sibling consumer of every drained record, not
/// downstream of the broadcaster. There is no snapshot seed: logs
/// are a pure live tail, not a keyed-state observation (contrast with
/// the keyed <c>ConfigSubscriptionBroadcaster</c> and
/// <c>LifecycleEventStream</c>, which seed the current
/// snapshot/started event into every new subscriber's buffer).
/// </remarks>
/// <param name="logger">Diagnostic sink for slow-subscriber
/// evictions.</param>
/// <exception cref="ArgumentNullException">
/// <paramref name="logger"/> is <see langword="null"/>.
/// </exception>
internal sealed class LogSubscriptionBroadcaster(ILogger<LogSubscriptionBroadcaster> logger)
    : SubscriptionBroadcaster<JsonLogRecord, LogSubscription>(logger, "logs-pipe")
{
    /// <inheritdoc/>
    protected override LogSubscription CreateSubscription(
        ChannelReader<JsonLogRecord> reader,
        Action release,
        Func<bool> wasEvicted)
        => new(reader, release, wasEvicted);
}
