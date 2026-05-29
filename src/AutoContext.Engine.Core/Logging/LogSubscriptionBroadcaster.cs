namespace AutoContext.Engine.Core.Logging;

using System.Threading.Channels;

using AutoContext.Engine.Core.Logging.Primitives;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging;

/// <summary>
/// Singleton fan-out broadcaster backing the engine's
/// <c>logs</c> named pipe (and the <c>Logs.TailEngine</c> RPC
/// stream, when wired): every connection that opens the
/// <c>logs</c> pipe calls <see cref="Subscribe"/> to receive a
/// per-subscriber bounded buffer of <see cref="LogRecord"/> values
/// drained by <see cref="LogFileSinkService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each subscriber owns its own bounded <see cref="Channel{T}"/>.
/// A slow subscriber is evicted with a terminal
/// <see cref="LogEvictedFrame"/> while the remaining subscribers
/// keep flowing. The file sink stays unaffected by subscriber
/// slowness — it is a sibling consumer of every drained record,
/// not downstream of the broadcaster.
/// </para>
/// <para>
/// There is no snapshot seed: logs are a pure live tail, not a
/// keyed-state observation (contrast with
/// <c>LifecycleEventStream</c>, which seeds the current
/// <c>started</c> event into every new subscriber's buffer).
/// </para>
/// <para>
/// Thread-safety: <see cref="Subscribe"/>, <see cref="TryPublish"/>,
/// <see cref="Complete"/>, and subscription disposal are safe to
/// invoke concurrently from any thread. Subscribers' read loops
/// are independent of the publisher and of one another.
/// </para>
/// </remarks>
internal sealed partial class LogSubscriptionBroadcaster
{
    /// <summary>
    /// Per-subscriber bounded buffer capacity. Sized to absorb a
    /// burst of records without evicting a healthy subscriber that
    /// is briefly behind on the wire; the terminal
    /// <see cref="LogEvictedFrame"/> kicks in only when a
    /// subscriber is sustainedly slower than the publisher.
    /// </summary>
    internal const int SubscriberBufferCapacity = 64;

    private bool _completed;
    private readonly Lock _gate = new();
    private readonly ILogger<LogSubscriptionBroadcaster> _logger;
    private readonly HashSet<LogSubscriber> _subscribers = [];

    /// <summary>
    /// Creates a new <see cref="LogSubscriptionBroadcaster"/>.
    /// </summary>
    /// <param name="logger">Diagnostic sink for slow-subscriber
    /// evictions and publish accounting.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public LogSubscriptionBroadcaster(ILogger<LogSubscriptionBroadcaster> logger)
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
    /// Enrolls a new subscriber and returns a
    /// <see cref="LogSubscription"/> the caller drains via
    /// <see cref="LogSubscription.ReadAllAsync"/>. Disposing the
    /// returned subscription unsubscribes and completes the
    /// underlying channel.
    /// </summary>
    /// <remarks>
    /// If the broadcaster has already completed, the new subscriber
    /// receives an immediately-completed channel — no records, no
    /// terminal frame.
    /// </remarks>
    public LogSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<LogRecord>(
            new BoundedChannelOptions(SubscriberBufferCapacity)
            {
                SingleReader = true,
                SingleWriter = false,

                // Wait mode (not DropWrite) is what makes the
                // eviction path live: under Wait, TryWrite returns
                // false on a full buffer (it never blocks — only
                // WriteAsync would), so the publisher can detect a
                // sustainedly slow subscriber and route it through
                // Evict. DropWrite would silently lose records and
                // never signal back, defeating the backpressure
                // contract.
                FullMode = BoundedChannelFullMode.Wait,

                // Explicit default: TryWrite must NOT inline a
                // waiting reader's continuation, because TryPublish
                // calls TryWrite while holding _gate. A synchronous
                // continuation would run the subscriber's reader
                // loop under our lock and (worse) could re-enter
                // the broadcaster (e.g. via Release on Dispose)
                // and deadlock. Pinning this to false makes the
                // safety guarantee explicit instead of relying on
                // the library default.
                AllowSynchronousContinuations = false,
            });

        var subscriber = new LogSubscriber(channel);

        lock (_gate)
        {
            if (_completed)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                _subscribers.Add(subscriber);
            }
        }

        return new LogSubscription(
            channel.Reader,
            release: () => Release(subscriber),
            wasEvicted: () => subscriber.WasEvicted);
    }

    /// <summary>
    /// Attempts to publish <paramref name="record"/> to every
    /// current subscriber. Slow subscribers (whose bounded buffer
    /// is full) are evicted; surviving subscribers keep flowing.
    /// Returns <see langword="false"/> if the broadcaster has
    /// already completed.
    /// </summary>
    /// <param name="record">The log record to fan out.</param>
    /// <returns>
    /// <see langword="true"/> if the record was accepted by the
    /// broadcaster; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryPublish(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var evictedCount = 0;

        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            foreach (var subscriber in _subscribers.ToArray())
            {
                if (subscriber.Channel.Writer.TryWrite(record))
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
        Message = "Evicted {EvictedCount} slow logs-pipe subscriber(s) after bounded buffer overflow.")]
    private static partial void LogSubscribersEvicted(ILogger logger, int evictedCount);

    private bool EvictCore(LogSubscriber subscriber)
    {
        if (!subscriber.TryEvict())
        {
            return false;
        }

        _subscribers.Remove(subscriber);
        subscriber.Channel.Writer.TryComplete();

        return true;
    }

    private void Release(LogSubscriber subscriber)
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
