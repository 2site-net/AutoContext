namespace AutoContext.Engine.Core.Logging.Primitives;

using System.Threading.Channels;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Internal per-subscriber state for
/// <see cref="LogSubscriptionBroadcaster"/>: the bounded channel
/// records are fanned into, plus an atomic state machine the
/// subscription's reader-side adapter consults to surface the
/// terminal <c>evicted</c> frame.
/// </summary>
internal sealed class LogSubscriber
{
    private const int Active = 0;
    private const int Closed = 1;
    private const int Evicted = 2;

    private int _state = Active;

    /// <summary>
    /// Creates a new subscriber bound to <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">Bounded channel the broadcaster writes
    /// records into and the subscription's reader loop drains.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channel"/> is <see langword="null"/>.
    /// </exception>
    public LogSubscriber(Channel<JsonLogRecord> channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        Channel = channel;
    }

    /// <summary>
    /// Bounded channel the broadcaster writes records into and the
    /// subscription's reader loop drains.
    /// </summary>
    public Channel<JsonLogRecord> Channel { get; }

    /// <summary>
    /// <see langword="true"/> once the broadcaster has dropped this
    /// subscriber for failing to keep up with the publisher.
    /// </summary>
    public bool WasEvicted
        => Volatile.Read(ref _state) == Evicted;

    /// <summary>
    /// Transitions the subscriber from active to normally closed.
    /// Returns <see langword="false"/> if the subscriber has already
    /// transitioned out of the active state.
    /// </summary>
    public bool TryClose()
        => Interlocked.CompareExchange(ref _state, Closed, Active) == Active;

    /// <summary>
    /// Transitions the subscriber from active to evicted. Returns
    /// <see langword="false"/> if the subscriber has already
    /// transitioned out of the active state.
    /// </summary>
    public bool TryEvict()
        => Interlocked.CompareExchange(ref _state, Evicted, Active) == Active;
}
