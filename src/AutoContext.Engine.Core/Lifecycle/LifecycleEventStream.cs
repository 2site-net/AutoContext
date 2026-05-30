namespace AutoContext.Engine.Core.Lifecycle;

using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Singleton fan-out stream backing <c>Engine.Lifecycle.Subscribe</c>:
/// every <c>events</c>-pipe connection that completes the
/// <c>Engine.Hello</c> handshake calls <see cref="Subscribe"/> to
/// receive a per-subscriber bounded buffer of
/// <see cref="JsonLifecycleEvent"/> values.
/// </summary>
/// <remarks>
/// <para>
/// Each subscriber owns its own bounded <see cref="Channel{T}"/>.
/// A slow subscriber is evicted with a terminal
/// <see cref="LifecycleEventKinds.Evicted"/> frame while the
/// remaining subscribers keep flowing.
/// </para>
/// <para>
/// The stream itself does not decide which lifecycle transition is
/// terminal. Callers publish ordinary lifecycle events with
/// <see cref="TryPublish"/> and complete the stream with
/// <see cref="TryComplete"/> when they have a terminal event to send.
/// </para>
/// <para>
/// Thread-safety: <see cref="Subscribe"/>, <see cref="TryPublish"/>,
/// <see cref="TryComplete"/>, and subscription disposal are safe to
/// invoke concurrently from any thread. Subscribers' read loops are
/// independent of the publisher and of one another.
/// </para>
/// </remarks>
internal sealed partial class LifecycleEventStream
{
    /// <summary>
    /// Per-subscriber bounded buffer capacity. Sized to absorb a
    /// burst of lifecycle transitions without evicting a healthy
    /// subscriber that is briefly behind on the wire; the terminal
    /// <see cref="LifecycleEventKinds.Evicted"/> frame kicks in only
    /// when a subscriber is sustainedly slower than the publisher.
    /// </summary>
    internal const int SubscriberBufferCapacity = 64;

    private bool _completed;
    private readonly Lock _gate = new();
    private readonly Guid _instanceId;
    private readonly ILogger<LifecycleEventStream> _logger;
    private readonly HashSet<LifecycleEventSubscriber> _subscribers = [];
    private JsonLifecycleEvent? _terminalEvent;

    /// <summary>
    /// Creates a new <see cref="LifecycleEventStream"/>.
    /// </summary>
    /// <param name="options">
    /// Engine options — used to stamp the owning
    /// <see cref="JsonLifecycleEvent.InstanceId"/> onto the seeded
    /// <see cref="LifecycleEventKinds.Started"/> event.
    /// </param>
    /// <param name="logger">
    /// Diagnostic sink for slow-subscriber evictions and publish
    /// accounting.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public LifecycleEventStream(
        IOptions<EngineOptions> options,
        ILogger<LifecycleEventStream> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _instanceId = options.Value.InstanceId;
        _logger = logger;
    }

    /// <summary>
    /// Enrolls a new subscriber, seeds it with the current
    /// <see cref="LifecycleEventKinds.Started"/> event, and returns a
    /// <see cref="LifecycleEventSubscription"/> the caller drains via
    /// <see cref="LifecycleEventSubscription.ReadAllAsync"/>. Disposing the returned
    /// subscription unsubscribes and completes the underlying channel.
    /// </summary>
    /// <remarks>
    /// If the stream has already completed, the new subscriber receives
    /// the seeded <c>started</c> event followed by the terminal event,
    /// then a completed channel — preserving the invariant that every
    /// subscriber observes the current snapshot key before the stream
    /// ends.
    /// </remarks>
    public LifecycleEventSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<JsonLifecycleEvent>(
            new BoundedChannelOptions(SubscriberBufferCapacity)
            {
                SingleReader = true,
                SingleWriter = false,

                // Wait mode (not DropWrite) is what makes the
                // eviction path live: under Wait, TryWrite returns
                // false on a full buffer (it never blocks — only
                // WriteAsync would), so the publisher can detect a
                // sustainedly slow subscriber and route it through
                // Evict. DropWrite would silently lose events and
                // never signal back, defeating the backpressure
                // contract.
                FullMode = BoundedChannelFullMode.Wait,
            });

        var subscriber = new LifecycleEventSubscriber(channel);

        // Seed the started event BEFORE registering so it lands at
        // the head of the buffer no matter what concurrent publishes
        // are racing.
        _ = channel.Writer.TryWrite(CreateStartedEvent());

        JsonLifecycleEvent? terminalEvent;

        lock (_gate)
        {
            if (_completed)
            {
                terminalEvent = _terminalEvent;
            }
            else
            {
                _subscribers.Add(subscriber);
                terminalEvent = null;
            }
        }

        if (terminalEvent is not null)
        {
            _ = channel.Writer.TryWrite(terminalEvent);
            subscriber.TryClose();
            channel.Writer.TryComplete();
        }

        return new LifecycleEventSubscription(
            channel.Reader,
            release: () => Release(subscriber),
            wasEvicted: () => subscriber.WasEvicted);
    }

    /// <summary>
    /// Attempts to publish <paramref name="terminalEvent"/> to every
    /// current subscriber and complete the stream. Idempotent: after
    /// the first successful completion, subsequent calls return
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="terminalEvent">The terminal lifecycle event.</param>
    /// <returns>
    /// <see langword="true"/> if this call completed the stream;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryComplete(JsonLifecycleEvent terminalEvent)
    {
        ArgumentNullException.ThrowIfNull(terminalEvent);

        var evictedCount = 0;
        var subscriberCount = 0;

        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            _completed = true;
            _terminalEvent = terminalEvent;

            foreach (var subscriber in _subscribers.ToArray())
            {
                subscriberCount++;

                if (!subscriber.Channel.Writer.TryWrite(terminalEvent))
                {
                    if (EvictCore(subscriber))
                    {
                        evictedCount++;
                    }

                    continue;
                }

                if (subscriber.TryClose())
                {
                    subscriber.Channel.Writer.TryComplete();
                }
            }

            _subscribers.Clear();
        }

        LogCompleted(_logger, terminalEvent.Kind, subscriberCount);

        if (evictedCount > 0)
        {
            LogSubscribersEvicted(_logger, evictedCount);
        }

        return true;
    }

    /// <summary>
    /// Attempts to publish <paramref name="evt"/> to every current
    /// subscriber. Returns <see langword="false"/> if the stream has
    /// already completed.
    /// </summary>
    /// <param name="evt">The lifecycle event to publish.</param>
    /// <returns>
    /// <see langword="true"/> if the event was accepted by the stream;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryPublish(JsonLifecycleEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var evictedCount = 0;
        var subscriberCount = 0;

        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            foreach (var subscriber in _subscribers.ToArray())
            {
                subscriberCount++;

                if (subscriber.Channel.Writer.TryWrite(evt))
                {
                    continue;
                }

                if (EvictCore(subscriber))
                {
                    evictedCount++;
                }
            }
        }

        LogPublished(_logger, evt.Kind, subscriberCount);

        if (evictedCount > 0)
        {
            LogSubscribersEvicted(_logger, evictedCount);
        }

        return true;
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Completed lifecycle event stream with '{Kind}' for {SubscriberCount} subscriber(s).")]
    private static partial void LogCompleted(
        ILogger logger,
        string kind,
        int subscriberCount);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Published lifecycle event '{Kind}' to {SubscriberCount} subscriber(s).")]
    private static partial void LogPublished(
        ILogger logger,
        string kind,
        int subscriberCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Evicted {EvictedCount} slow Engine.Lifecycle subscriber(s) after bounded buffer overflow.")]
    private static partial void LogSubscribersEvicted(ILogger logger, int evictedCount);

    private JsonLifecycleEvent CreateStartedEvent()
    {
        return new JsonLifecycleEvent
        {
            Kind = LifecycleEventKinds.Started,
            InstanceId = _instanceId,
            Revision = 0,
        };
    }

    private bool EvictCore(LifecycleEventSubscriber subscriber)
    {
        if (!subscriber.TryEvict())
        {
            return false;
        }

        _subscribers.Remove(subscriber);
        subscriber.Channel.Writer.TryComplete();

        return true;
    }

    private void Release(LifecycleEventSubscriber subscriber)
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
