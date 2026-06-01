namespace AutoContext.Engine.Core.Workspace.Config;

using System.Threading.Channels;

using AutoContext.Engine.Core.Workspace.Config.Primitives;
using AutoContext.Engine.Protocol.Messages.Config;

using Microsoft.Extensions.Logging;

/// <summary>
/// Singleton fan-out broadcaster backing the engine's
/// <c>Config.Subscribe</c> RPC stream: every connection that opens
/// the stream calls <see cref="Subscribe"/> to receive a
/// per-subscriber bounded buffer of <see cref="JsonConfigSnapshot"/>
/// values, seeded with the current snapshot so a late subscriber
/// observes the live state without a separate <c>Config.Get</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the pure live tail of <c>LogSubscriptionBroadcaster</c>,
/// config is keyed state: the broadcaster caches the latest snapshot
/// and replays it as the first frame to every new subscriber
/// (snapshot-on-subscribe, mirroring <c>LifecycleEventStream</c>'s
/// started-event seed). The cache is primed once at startup via
/// <see cref="Prime"/> — the config manager's initial disk load does
/// not raise a change event, so the seed must be supplied explicitly
/// before the watcher and the first subscriber go live.
/// </para>
/// <para>
/// Each subscriber owns its own bounded <see cref="Channel{T}"/>. A
/// slow subscriber is evicted with a terminal
/// <see cref="JsonConfigEvictedFrame"/> while the remaining
/// subscribers keep flowing.
/// </para>
/// <para>
/// Thread-safety: <see cref="Prime"/>, <see cref="Subscribe"/>,
/// <see cref="TryPublish"/>, <see cref="Complete"/>, and
/// subscription disposal are safe to invoke concurrently from any
/// thread. Subscribers' read loops are independent of the publisher
/// and of one another.
/// </para>
/// </remarks>
internal sealed partial class ConfigSubscriptionBroadcaster
{
    /// <summary>
    /// Per-subscriber bounded buffer capacity. Sized to absorb a
    /// burst of snapshots without evicting a healthy subscriber that
    /// is briefly behind on the wire; the terminal
    /// <see cref="JsonConfigEvictedFrame"/> kicks in only when a
    /// subscriber is sustainedly slower than the publisher.
    /// </summary>
    internal const int SubscriberBufferCapacity = 64;

    private bool _completed;
    private readonly Lock _gate = new();
    private JsonConfigSnapshot? _latest;
    private readonly ILogger<ConfigSubscriptionBroadcaster> _logger;
    private readonly HashSet<ConfigSubscriber> _subscribers = [];

    /// <summary>
    /// Creates a new <see cref="ConfigSubscriptionBroadcaster"/>.
    /// </summary>
    /// <param name="logger">Diagnostic sink for slow-subscriber
    /// evictions and publish accounting.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public ConfigSubscriptionBroadcaster(ILogger<ConfigSubscriptionBroadcaster> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <summary>
    /// Marks the broadcaster as completed and closes every active
    /// subscriber's channel. Subscribers observe EOF (no terminal
    /// frame) — graceful shutdown is not an eviction. Idempotent:
    /// subsequent calls return without effect.
    /// </summary>
    public void Complete()
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            foreach (var subscriber in _subscribers)
            {
                if (subscriber.TryClose())
                {
                    subscriber.Channel.Writer.TryComplete();
                }
            }

            _subscribers.Clear();
        }
    }

    /// <summary>
    /// Seeds the cached latest snapshot without fanning it out.
    /// Called once at startup so the first subscriber's
    /// snapshot-on-subscribe frame reflects the disk-loaded state
    /// (the config manager's initial load raises no change event).
    /// No-op once the broadcaster has completed.
    /// </summary>
    /// <param name="snapshot">Current config snapshot to cache.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshot"/> is <see langword="null"/>.
    /// </exception>
    public void Prime(JsonConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _latest = snapshot;
        }
    }

    /// <summary>
    /// Enrolls a new subscriber and returns a
    /// <see cref="ConfigSubscription"/> the caller drains via
    /// <see cref="ConfigSubscription.ReadAllAsync"/>. The current
    /// cached snapshot, if any, is seeded as the subscriber's first
    /// frame before registration so it lands at the head of the
    /// buffer no matter what concurrent publishes are racing.
    /// Disposing the returned subscription unsubscribes and
    /// completes the underlying channel.
    /// </summary>
    /// <remarks>
    /// If the broadcaster has already completed, the new subscriber
    /// receives the seed (when present) followed by an immediate EOF
    /// — no further frames.
    /// </remarks>
    public ConfigSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<JsonConfigSnapshot>(
            new BoundedChannelOptions(SubscriberBufferCapacity)
            {
                SingleReader = true,
                SingleWriter = false,

                // Wait mode (not DropWrite) is what makes the
                // eviction path live: under Wait, TryWrite returns
                // false on a full buffer so the publisher can detect
                // a sustainedly slow subscriber and route it through
                // Evict instead of silently losing snapshots.
                FullMode = BoundedChannelFullMode.Wait,

                // Explicit default: TryWrite must NOT inline a
                // waiting reader's continuation, because TryPublish
                // and the seed write below run while holding _gate.
                // A synchronous continuation would run the
                // subscriber's reader loop under our lock and could
                // re-enter the broadcaster (e.g. via Release on
                // Dispose) and deadlock.
                AllowSynchronousContinuations = false,
            });

        var subscriber = new ConfigSubscriber(channel);

        lock (_gate)
        {
            // Seed the cached snapshot BEFORE registering so it
            // lands at the head of this subscriber's buffer ahead of
            // any publish that races in once registration completes.
            if (_latest is not null)
            {
                channel.Writer.TryWrite(_latest);
            }

            if (_completed)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                _subscribers.Add(subscriber);
            }
        }

        return new ConfigSubscription(
            channel.Reader,
            release: () => Release(subscriber),
            wasEvicted: () => subscriber.WasEvicted);
    }

    /// <summary>
    /// Caches <paramref name="snapshot"/> as the latest state and
    /// attempts to publish it to every current subscriber. Slow
    /// subscribers (whose bounded buffer is full) are evicted;
    /// surviving subscribers keep flowing. Returns
    /// <see langword="false"/> if the broadcaster has already
    /// completed.
    /// </summary>
    /// <param name="snapshot">The config snapshot to fan out.</param>
    /// <returns>
    /// <see langword="true"/> if the snapshot was accepted by the
    /// broadcaster; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshot"/> is <see langword="null"/>.
    /// </exception>
    public bool TryPublish(JsonConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var evictedCount = 0;

        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            _latest = snapshot;

            foreach (var subscriber in _subscribers.ToArray())
            {
                if (subscriber.Channel.Writer.TryWrite(snapshot))
                {
                    continue;
                }

                if (EvictCore(subscriber))
                {
                    evictedCount++;
                }
            }
        }

        if (evictedCount > 0)
        {
            LogSubscribersEvicted(_logger, evictedCount);
        }

        return true;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Evicted {EvictedCount} slow Config.Subscribe subscriber(s) after bounded buffer overflow.")]
    private static partial void LogSubscribersEvicted(ILogger logger, int evictedCount);

    private bool EvictCore(ConfigSubscriber subscriber)
    {
        if (!subscriber.TryEvict())
        {
            return false;
        }

        _subscribers.Remove(subscriber);
        subscriber.Channel.Writer.TryComplete();

        return true;
    }

    private void Release(ConfigSubscriber subscriber)
    {
        lock (_gate)
        {
            if (!subscriber.TryClose())
            {
                return;
            }

            _subscribers.Remove(subscriber);
            subscriber.Channel.Writer.TryComplete();
        }
    }
}
