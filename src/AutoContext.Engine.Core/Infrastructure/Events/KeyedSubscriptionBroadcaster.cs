namespace AutoContext.Engine.Core.Infrastructure.Events;

using System.Threading.Channels;

using Microsoft.Extensions.Logging;

/// <summary>
/// A <see cref="SubscriptionBroadcaster{TPayload, TSubscription}"/>
/// that observes keyed state: it caches the latest published payload
/// and replays it as the first frame to every new subscriber
/// (snapshot-on-subscribe). The cache is primed once at startup via
/// <see cref="Prime"/> for sources whose initial load does not raise
/// a change event.
/// </summary>
/// <typeparam name="TPayload">Keyed payload fanned out to
/// subscribers.</typeparam>
/// <typeparam name="TSubscription">Domain handle returned from
/// <see cref="SubscriptionBroadcaster{TPayload, TSubscription}.Subscribe"/>.</typeparam>
/// <param name="logger">Diagnostic sink for slow-subscriber
/// evictions.</param>
/// <param name="channel">Channel label stamped onto the evict
/// warning's <c>{Channel}</c> property.</param>
/// <exception cref="ArgumentNullException">
/// <paramref name="logger"/> is <see langword="null"/>.
/// </exception>
/// <exception cref="ArgumentException">
/// <paramref name="channel"/> is empty.
/// </exception>
internal abstract class KeyedSubscriptionBroadcaster<TPayload, TSubscription>(
    ILogger logger, string channel)
    : SubscriptionBroadcaster<TPayload, TSubscription>(logger, channel)
    where TPayload : class
    where TSubscription : Subscription<TPayload>
{
    private TPayload? _latest;

    /// <summary>
    /// Seeds the cached latest payload without fanning it out. Called
    /// once at startup so the first subscriber's snapshot-on-subscribe
    /// frame reflects the initial state when the source's initial load
    /// raises no change event. No-op once the broadcaster has
    /// completed.
    /// </summary>
    /// <param name="snapshot">Current payload to cache.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshot"/> is <see langword="null"/>.
    /// </exception>
    public void Prime(TPayload snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (Gate)
        {
            if (IsCompleted)
            {
                return;
            }

            _latest = snapshot;
        }
    }

    /// <inheritdoc/>
    protected override void OnPublishing(TPayload payload)
        => _latest = payload;

    /// <inheritdoc/>
    protected override void OnSubscribing(ChannelWriter<TPayload> writer)
    {
        if (_latest is not null)
        {
            writer.TryWrite(_latest);
        }
    }
}
