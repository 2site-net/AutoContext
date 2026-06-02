namespace AutoContext.Engine.Core.Infrastructure.Events;

using System.Threading.Channels;

using Microsoft.Extensions.Logging;

/// <summary>
/// Singleton fan-out broadcaster: every subscriber owns its own bounded
/// <see cref="Channel{T}"/>. A slow subscriber is dropped while the rest keep
/// flowing, and graceful completion closes every channel without a terminal
/// frame. <see cref="Subscribe"/> returns the
/// <see cref="BroadcasterSubscription{T}"/> handle the caller drains; an
/// optional seed is written to the new subscriber's buffer ahead of the live
/// tail.
/// </summary>
/// <remarks>
/// Thread-safety: <see cref="Subscribe"/>, <see cref="TryPublish"/>,
/// <see cref="Complete"/>, and subscription disposal are safe to
/// invoke concurrently from any thread. Subscribers' read loops are
/// independent of the publisher and of one another.
/// </remarks>
/// <typeparam name="TPayload">Payload fanned out to subscribers.</typeparam>
internal sealed class Broadcaster<TPayload>
    where TPayload : class
{
    /// <summary>
    /// Per-subscriber bounded buffer capacity. Sized to absorb a
    /// burst of payloads without dropping a healthy subscriber that
    /// is briefly behind on the wire; dropping kicks in only when a
    /// subscriber is sustainedly slower than the publisher.
    /// </summary>
    internal const int SubscriberBufferCapacity = 64;

    private readonly string _channel;
    private bool _completed;
    private readonly Lock _gate = new();
    private readonly ILogger _logger;
    private readonly HashSet<BroadcasterSubscriber<TPayload>> _subscribers = [];

    /// <summary>
    /// Creates a new
    /// <see cref="Broadcaster{T}"/>.
    /// </summary>
    /// <param name="logger">Diagnostic sink for slow-subscriber
    /// drops.</param>
    /// <param name="channel">Channel label stamped onto the drop
    /// warning's <c>{Channel}</c> property (e.g. <c>logs-pipe</c>).</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="channel"/> is empty.
    /// </exception>
    public Broadcaster(ILogger logger, string channel)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrEmpty(channel);

        _logger = logger;
        _channel = channel;
    }

    /// <summary>
    /// Marks the broadcaster as completed and closes every active
    /// subscriber's channel. Subscribers observe EOF (no terminal
    /// frame) — graceful shutdown is not a drop. Idempotent:
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
    /// Enrolls a new subscriber and returns the
    /// <see cref="BroadcasterSubscription{T}"/> the caller drains. Disposing the
    /// returned subscription unsubscribes and completes the underlying
    /// channel.
    /// </summary>
    /// <remarks>
    /// If the broadcaster has already completed, the new subscriber
    /// receives the <paramref name="seed"/> payloads followed by an
    /// immediate EOF — no further frames.
    /// </remarks>
    /// <param name="seed">Payloads written to the new subscriber's
    /// buffer, in order, ahead of the live tail. Empty for a pure
    /// live-tail subscription.</param>
    /// <returns>A new subscription handle.</returns>
    public BroadcasterSubscription<TPayload> Subscribe(params ReadOnlySpan<TPayload> seed)
    {
        var channel = Channel.CreateBounded<TPayload>(
            new BoundedChannelOptions(SubscriberBufferCapacity)
            {
                SingleReader = true,
                SingleWriter = false,

                // Wait mode (not DropWrite) is what makes the
                // drop path live: under Wait, TryWrite returns
                // false on a full buffer (it never blocks — only
                // WriteAsync would), so the publisher can detect a
                // sustainedly slow subscriber and route it through
                // Drop. DropWrite would silently lose payloads and
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

        var subscriber = new BroadcasterSubscriber<TPayload>(channel);

        lock (_gate)
        {
            // Seed BEFORE registering so the seed payloads land at
            // the head of this subscriber's buffer ahead of any
            // publish that races in once registration completes.
            foreach (var payload in seed)
            {
                channel.Writer.TryWrite(payload);
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

        return new BroadcasterSubscription<TPayload>(
            channel.Reader,
            release: () => Release(subscriber),
            wasDropped: () => subscriber.WasDropped);
    }

    /// <summary>
    /// Attempts to publish <paramref name="payload"/> to every
    /// current subscriber. Slow subscribers (whose bounded buffer is
    /// full) are dropped; surviving subscribers keep flowing. Returns
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

        var droppedCount = 0;

        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            foreach (var subscriber in _subscribers.ToArray())
            {
                if (subscriber.Channel.Writer.TryWrite(payload))
                {
                    continue;
                }

                if (DropCore(subscriber))
                {
                    droppedCount++;
                }
            }
        }

        if (droppedCount > 0)
        {
            BroadcasterLog.SubscribersDropped(
                _logger, droppedCount, _channel);
        }

        return true;
    }

    private bool DropCore(BroadcasterSubscriber<TPayload> subscriber)
    {
        if (!subscriber.TryDrop())
        {
            return false;
        }

        _subscribers.Remove(subscriber);
        subscriber.Channel.Writer.TryComplete();

        return true;
    }

    private void Release(BroadcasterSubscriber<TPayload> subscriber)
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
