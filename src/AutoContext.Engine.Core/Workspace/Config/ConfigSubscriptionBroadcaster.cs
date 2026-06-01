namespace AutoContext.Engine.Core.Workspace.Config;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Config;

using Microsoft.Extensions.Logging;

/// <summary>
/// Singleton fan-out broadcaster backing the engine's
/// <c>Config.Subscribe</c> RPC stream: every connection that opens
/// the stream calls
/// <see cref="SubscriptionBroadcaster{TPayload, TSubscription}.Subscribe"/>
/// to receive a per-subscriber bounded buffer of
/// <see cref="JsonConfigSnapshot"/> values, seeded with the current
/// snapshot so a late subscriber observes the live state without a
/// separate <c>Config.Get</c>.
/// </summary>
/// <remarks>
/// Unlike the pure live tail of <c>LogSubscriptionBroadcaster</c>,
/// config is keyed state: the broadcaster caches the latest snapshot
/// and replays it as the first frame to every new subscriber
/// (snapshot-on-subscribe, mirroring <c>LifecycleEventStream</c>'s
/// started-event seed). The cache is primed once at startup via
/// <see cref="KeyedSubscriptionBroadcaster{TPayload, TSubscription}.Prime"/>
/// — the config manager's initial disk load does not raise a change
/// event, so the seed must be supplied explicitly before the watcher
/// and the first subscriber go live. Each subscriber owns its own
/// bounded <see cref="Channel{T}"/>; a slow subscriber is evicted
/// with a terminal <see cref="JsonConfigEvictedFrame"/> while the
/// remaining subscribers keep flowing.
/// </remarks>
/// <param name="logger">Diagnostic sink for slow-subscriber
/// evictions.</param>
/// <exception cref="ArgumentNullException">
/// <paramref name="logger"/> is <see langword="null"/>.
/// </exception>
internal sealed class ConfigSubscriptionBroadcaster(ILogger<ConfigSubscriptionBroadcaster> logger)
    : KeyedSubscriptionBroadcaster<JsonConfigSnapshot, ConfigSubscription>(logger, "Config.Subscribe")
{
    /// <inheritdoc/>
    protected override ConfigSubscription CreateSubscription(
        ChannelReader<JsonConfigSnapshot> reader,
        Action release,
        Func<bool> wasEvicted)
        => new(reader, release, wasEvicted);
}
