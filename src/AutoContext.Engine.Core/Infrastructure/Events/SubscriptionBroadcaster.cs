namespace AutoContext.Engine.Core.Infrastructure.Events;

using System.Threading.Channels;

using Microsoft.Extensions.Logging;

/// <summary>
/// Singleton fan-out broadcaster core: every subscriber owns its own
/// bounded <see cref="Channel{T}"/>, a slow subscriber is evicted
/// while the rest keep flowing, and graceful completion closes every
/// channel without a terminal frame. Derived types supply the
/// per-domain <see cref="Subscription{T}"/> via
/// <see cref="CreateSubscription"/> and may seed/observe keyed state
/// through <see cref="OnSubscribing"/> / <see cref="OnPublishing"/>.
/// </summary>
/// <remarks>
/// Thread-safety: <see cref="Subscribe"/>, <see cref="TryPublish"/>,
/// <see cref="Complete"/>, and subscription disposal are safe to
/// invoke concurrently from any thread. Subscribers' read loops are
/// independent of the publisher and of one another.
/// </remarks>
/// <typeparam name="TPayload">Payload fanned out to subscribers.</typeparam>
/// <typeparam name="TSubscription">Domain handle returned from
/// <see cref="Subscribe"/>.</typeparam>
internal abstract class SubscriptionBroadcaster<TPayload, TSubscription>
    where TPayload : class
    where TSubscription : Subscription<TPayload>
{
    /// <summary>
    /// Per-subscriber bounded buffer capacity. Sized to absorb a
    /// burst of payloads without evicting a healthy subscriber that
    /// is briefly behind on the wire; eviction kicks in only when a
    /// subscriber is sustainedly slower than the publisher.
    /// </summary>
    internal const int SubscriberBufferCapacity = 64;

    private readonly string _channel;
    private bool _completed;
    private readonly Lock _gate = new();
    private readonly ILogger _logger;
    private readonly HashSet<Subscriber<TPayload>> _subscribers = [];

    /// <summary>
    /// Creates a new
    /// <see cref="SubscriptionBroadcaster{TPayload, TSubscription}"/>.
    /// </summary>
    /// <param name="logger">Diagnostic sink for slow-subscriber
    /// evictions.</param>
    /// <param name="channel">Channel label stamped onto the evict
    /// warning's <c>{Channel}</c> property (e.g. <c>logs-pipe</c>).</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="channel"/> is empty.
    /// </exception>
    protected SubscriptionBroadcaster(ILogger logger, string channel)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrEmpty(channel);

        _logger = logger;
        _channel = channel;
    }

    /// <summary>
    /// Synchronization gate guarding subscriber set mutations and the
    /// completion flag. Derived types lock on this when seeding or
    /// reading keyed state so their writes are ordered against
    /// <see cref="Subscribe"/> and <see cref="TryPublish"/>.
    /// </summary>
    protected Lock Gate
        => _gate;

    /// <summary>
    /// <see langword="true"/> once <see cref="Complete"/> has run.
    /// Read by derived types under <see cref="Gate"/>.
    /// </summary>
    protected bool IsCompleted
        => _completed;

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
    /// Enrolls a new subscriber and returns the domain
    /// <typeparamref name="TSubscription"/> the caller drains.
    /// Disposing the returned subscription unsubscribes and completes
    /// the underlying channel.
    /// </summary>
    /// <remarks>
    /// If the broadcaster has already completed, the new subscriber
    /// receives any seed written by <see cref="OnSubscribing"/>
    /// followed by an immediate EOF — no further frames.
    /// </remarks>
    /// <returns>A new domain subscription handle.</returns>
    public TSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<TPayload>(
            new BoundedChannelOptions(SubscriberBufferCapacity)
            {
                SingleReader = true,
                SingleWriter = false,

                // Wait mode (not DropWrite) is what makes the
                // eviction path live: under Wait, TryWrite returns
                // false on a full buffer (it never blocks — only
                // WriteAsync would), so the publisher can detect a
                // sustainedly slow subscriber and route it through
                // Evict. DropWrite would silently lose payloads and
                // never signal back, defeating the backpressure
                // contract.
                FullMode = BoundedChannelFullMode.Wait,

                // Explicit default: TryWrite must NOT inline a
                // waiting reader's continuation, because the publish
                // fan-out (and any keyed snapshot seed) runs TryWrite
                // while holding _gate. A synchronous continuation
                // would run the subscriber's reader loop under our
                // lock and (worse) could re-enter the broadcaster
                // (e.g. via Release on Dispose) and deadlock. Pinning
                // this to false makes the safety guarantee explicit
                // instead of relying on the library default.
                AllowSynchronousContinuations = false,
            });

        var subscriber = new Subscriber<TPayload>(channel);

        lock (_gate)
        {
            // Seed keyed state (if any) BEFORE registering so it
            // lands at the head of this subscriber's buffer ahead of
            // any publish that races in once registration completes.
            OnSubscribing(channel.Writer);

            if (_completed)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                _subscribers.Add(subscriber);
            }
        }

        return CreateSubscription(
            channel.Reader,
            release: () => Release(subscriber),
            wasEvicted: () => subscriber.WasEvicted);
    }

    /// <summary>
    /// Attempts to publish <paramref name="payload"/> to every
    /// current subscriber. Slow subscribers (whose bounded buffer is
    /// full) are evicted; surviving subscribers keep flowing. Returns
    /// <see langword="false"/> if the broadcaster has already
    /// completed.
    /// </summary>
    /// <param name="payload">The payload to fan out.</param>
    /// <returns>
    /// <see langword="true"/> if the payload was accepted by the
    /// broadcaster; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="payload"/> is <see langword="null"/>.
    /// </exception>
    public bool TryPublish(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var evictedCount = 0;

        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            OnPublishing(payload);

            foreach (var subscriber in _subscribers.ToArray())
            {
                if (subscriber.Channel.Writer.TryWrite(payload))
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
            SubscriptionBroadcasterLog.SubscribersEvicted(
                _logger, evictedCount, _channel);
        }

        return true;
    }

    /// <summary>
    /// Creates the domain subscription handle wrapping the supplied
    /// channel reader, release callback, and eviction probe.
    /// </summary>
    /// <param name="reader">Reader half of the subscriber's
    /// channel.</param>
    /// <param name="release">Unsubscribe callback for
    /// <see cref="IDisposable.Dispose"/>.</param>
    /// <param name="wasEvicted">Eviction probe consulted after the
    /// drain completes.</param>
    /// <returns>A new domain subscription.</returns>
    protected abstract TSubscription CreateSubscription(
        ChannelReader<TPayload> reader,
        Action release,
        Func<bool> wasEvicted);

    /// <summary>
    /// Called under <see cref="Gate"/> at the start of a publish,
    /// giving keyed broadcasters a chance to cache the latest
    /// payload. The base implementation is a no-op (no keyed state).
    /// </summary>
    /// <param name="payload">The payload about to be fanned out.</param>
    protected virtual void OnPublishing(TPayload payload)
    {
    }

    /// <summary>
    /// Called under <see cref="Gate"/> just before a new subscriber
    /// is registered, giving keyed broadcasters a chance to seed the
    /// subscriber's buffer with the cached snapshot. The base
    /// implementation is a no-op (pure live tail).
    /// </summary>
    /// <param name="writer">Writer half of the new subscriber's
    /// channel.</param>
    protected virtual void OnSubscribing(ChannelWriter<TPayload> writer)
    {
    }

    private bool EvictCore(Subscriber<TPayload> subscriber)
    {
        if (!subscriber.TryEvict())
        {
            return false;
        }

        _subscribers.Remove(subscriber);
        subscriber.Channel.Writer.TryComplete();

        return true;
    }

    private void Release(Subscriber<TPayload> subscriber)
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
