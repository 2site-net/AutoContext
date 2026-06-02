namespace AutoContext.Engine.Core.Infrastructure.Events;

using Microsoft.Extensions.Logging;

/// <summary>
/// A snapshot-on-subscribe broadcaster: wraps a
/// <see cref="Broadcaster{T}"/>, caches the latest
/// published payload, and replays it as the first frame to every new
/// subscriber. The cache is primed once at startup via
/// <see cref="Prime"/> for sources whose initial load does not raise
/// a change event.
/// </summary>
/// <remarks>
/// Thread-safety: <see cref="Prime"/>, <see cref="Subscribe"/>,
/// <see cref="TryPublish"/>, and <see cref="Complete"/> are safe to
/// invoke concurrently. The wrapper's gate is always taken before the
/// inner broadcaster's gate (never the reverse), so the cached
/// snapshot and the subscriber enrollment it seeds are observed
/// atomically with respect to publishes.
/// </remarks>
/// <typeparam name="TPayload">Keyed payload fanned out to
/// subscribers.</typeparam>
/// <param name="logger">Diagnostic sink for slow-subscriber
/// drops.</param>
/// <param name="channel">Channel label stamped onto the drop
/// warning's <c>{Channel}</c> property.</param>
/// <exception cref="ArgumentNullException">
/// <paramref name="logger"/> is <see langword="null"/>.
/// </exception>
/// <exception cref="ArgumentException">
/// <paramref name="channel"/> is empty.
/// </exception>
internal sealed class SnapshotBroadcaster<TPayload>(ILogger logger, string channel)
    where TPayload : class
{
    private readonly Broadcaster<TPayload> _core = new(logger, channel);
    private readonly Lock _gate = new();
    private bool _isCompleted;
    private TPayload? _latest;

    /// <summary>
    /// Marks the broadcaster as completed and closes every active
    /// subscriber's channel. Idempotent.
    /// </summary>
    public void Complete()
    {
        lock (_gate)
        {
            _isCompleted = true;
        }

        _core.Complete();
    }

    /// <summary>
    /// Seeds the cached latest payload without fanning it out. Called
    /// once at startup so the first subscriber's snapshot-on-subscribe
    /// frame reflects the initial state when the source's initial load
    /// raises no change event.
    /// </summary>
    /// <param name="snapshot">Current payload to cache.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshot"/> is <see langword="null"/>.
    /// </exception>
    public void Prime(TPayload snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            if (_isCompleted)
            {
                return;
            }

            _latest = snapshot;
        }
    }

    /// <summary>
    /// Enrolls a new subscriber, seeding it with the cached latest
    /// payload (if any) ahead of the live tail.
    /// </summary>
    /// <returns>A new subscription handle.</returns>
    public BroadcasterSubscription<TPayload> Subscribe()
    {
        lock (_gate)
        {
            return _latest is null
                ? _core.Subscribe()
                : _core.Subscribe(_latest);
        }
    }

    /// <summary>
    /// Fans <paramref name="payload"/> out to every current subscriber
    /// and, on success, caches it as the latest snapshot for future
    /// subscribers.
    /// </summary>
    /// <param name="payload">The payload to fan out.</param>
    /// <returns>
    /// <see langword="true"/> if the payload was accepted; otherwise,
    /// <see langword="false"/> (the broadcaster has completed).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="payload"/> is <see langword="null"/>.
    /// </exception>
    public bool TryPublish(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        lock (_gate)
        {
            var published = _core.TryPublish(payload);
            if (published)
            {
                _latest = payload;
            }

            return published;
        }
    }
}
