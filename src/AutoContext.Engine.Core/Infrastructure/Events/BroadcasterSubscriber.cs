namespace AutoContext.Engine.Core.Infrastructure.Events;

using System.Threading.Channels;

/// <summary>
/// Internal per-subscriber state for
/// <see cref="Broadcaster{T}"/>: the
/// bounded channel payloads are fanned into, plus an atomic state
/// machine the subscription's reader-side adapter consults to surface
/// the terminal <c>dropped</c> frame.
/// </summary>
/// <typeparam name="T">Payload type fanned into the channel.</typeparam>
internal sealed class BroadcasterSubscriber<T>
{
    private const int Active = 0;
    private const int Closed = 1;
    private const int Dropped = 2;

    private int _state = Active;

    /// <summary>
    /// Creates a new subscriber bound to <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">Bounded channel the broadcaster writes
    /// payloads into and the subscription's reader loop drains.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channel"/> is <see langword="null"/>.
    /// </exception>
    public BroadcasterSubscriber(Channel<T> channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        Channel = channel;
    }

    /// <summary>
    /// Bounded channel the broadcaster writes payloads into and the
    /// subscription's reader loop drains.
    /// </summary>
    public Channel<T> Channel { get; }

    /// <summary>
    /// <see langword="true"/> once the broadcaster has dropped this
    /// subscriber for failing to keep up with the publisher.
    /// </summary>
    public bool WasDropped
        => Volatile.Read(ref _state) == Dropped;

    /// <summary>
    /// Transitions the subscriber from active to normally closed.
    /// Returns <see langword="false"/> if the subscriber has already
    /// transitioned out of the active state.
    /// </summary>
    public bool TryClose()
        => Interlocked.CompareExchange(ref _state, Closed, Active) == Active;

    /// <summary>
    /// Transitions the subscriber from active to dropped. Returns
    /// <see langword="false"/> if the subscriber has already
    /// transitioned out of the active state.
    /// </summary>
    public bool TryDrop()
        => Interlocked.CompareExchange(ref _state, Dropped, Active) == Active;
}
