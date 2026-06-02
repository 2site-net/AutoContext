namespace AutoContext.Engine.Core.Lifecycle;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Singleton fan-out stream backing <c>Engine.Lifecycle.Subscribe</c>:
/// every <c>events</c>-pipe connection that completes the
/// <c>Engine.Hello</c> handshake calls <see cref="Subscribe"/> to
/// receive a per-subscriber bounded buffer of
/// <see cref="JsonLifecycleEvent"/> values. The fan-out, drop,
/// and completion mechanics are delegated to a wrapped
/// <see cref="Broadcaster{T}"/>; this type layers on the
/// lifecycle-specific <c>started</c> seed and terminal-event replay.
/// </summary>
/// <remarks>
/// <para>
/// The stream itself does not decide which lifecycle transition is
/// terminal. Callers publish ordinary lifecycle events with
/// <see cref="TryPublish"/> and complete the stream with
/// <see cref="TryComplete"/> when they have a terminal event to send.
/// A slow subscriber is dropped by the underlying broadcaster (which
/// surfaces a terminal <see cref="LifecycleEventKinds.Dropped"/> frame
/// downstream via <see cref="LifecycleFrameStream"/>) while the
/// remaining subscribers keep flowing.
/// </para>
/// <para>
/// Thread-safety: <see cref="Subscribe"/>, <see cref="TryPublish"/>,
/// <see cref="TryComplete"/>, and subscription disposal are safe to
/// invoke concurrently from any thread. The stream's gate is always
/// taken before the wrapped broadcaster's gate (never the reverse),
/// so the retained terminal event and the subscriber enrollment it
/// seeds are observed atomically with respect to completion.
/// </para>
/// </remarks>
internal sealed partial class LifecycleEventStream
{
    /// <summary>
    /// Per-subscriber bounded buffer capacity, delegated to the
    /// wrapped <see cref="Broadcaster{T}"/>.
    /// </summary>
    internal const int SubscriberBufferCapacity =
        Broadcaster<JsonLifecycleEvent>.SubscriberBufferCapacity;

    private readonly Broadcaster<JsonLifecycleEvent> _core;
    private bool _completed;
    private readonly Lock _gate = new();
    private readonly Guid _instanceId;
    private readonly ILogger<LifecycleEventStream> _logger;
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
    /// Diagnostic sink for publish accounting (slow-subscriber
    /// drops are logged by the wrapped broadcaster).
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
        _core = new Broadcaster<JsonLifecycleEvent>(
            logger, "Engine.Lifecycle");
    }

    /// <summary>
    /// Enrolls a new subscriber, seeds it with the current
    /// <see cref="LifecycleEventKinds.Started"/> event, and returns a
    /// <see cref="BroadcasterSubscription{T}"/> the caller drains via
    /// <see cref="LifecycleFrameStream.StreamAsync"/>. Disposing the returned
    /// subscription unsubscribes and completes the underlying channel.
    /// </summary>
    /// <remarks>
    /// If the stream has already completed, the new subscriber receives
    /// the seeded <c>started</c> event followed by the terminal event,
    /// then a completed channel — preserving the invariant that every
    /// subscriber observes the current snapshot key before the stream
    /// ends.
    /// </remarks>
    public BroadcasterSubscription<JsonLifecycleEvent> Subscribe()
    {
        var started = CreateStartedEvent();

        lock (_gate)
        {
            return _terminalEvent is null
                ? _core.Subscribe(started)
                : _core.Subscribe(started, _terminalEvent);
        }
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

        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            _completed = true;
            _terminalEvent = terminalEvent;

            // Fan the terminal event to live subscribers, then close
            // the broadcaster so they observe it ahead of EOF. Late
            // subscribers replay it from the retained _terminalEvent.
            _core.TryPublish(terminalEvent);
            _core.Complete();
        }

        LogCompleted(_logger, terminalEvent.Kind);
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

        var published = _core.TryPublish(evt);
        if (published)
        {
            LogPublished(_logger, evt.Kind);
        }

        return published;
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Completed lifecycle event stream with '{Kind}'.")]
    private static partial void LogCompleted(ILogger logger, string kind);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Published lifecycle event '{Kind}'.")]
    private static partial void LogPublished(ILogger logger, string kind);

    private JsonLifecycleEvent CreateStartedEvent()
    {
        return new JsonLifecycleEvent
        {
            Kind = LifecycleEventKinds.Started,
            InstanceId = _instanceId,
            Revision = 0,
        };
    }
}
