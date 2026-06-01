namespace AutoContext.Engine.Core.Workspace.Config.Primitives;

using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Config;

/// <summary>
/// Internal per-subscriber state for
/// <see cref="ConfigSubscriptionBroadcaster"/>: the bounded channel
/// snapshots are fanned into, plus an atomic state machine the
/// subscription's reader-side adapter consults to surface the
/// terminal <c>evicted</c> frame.
/// </summary>
internal sealed class ConfigSubscriber
{
    private const int Active = 0;
    private const int Closed = 1;
    private const int Evicted = 2;

    private int _state = Active;

    /// <summary>
    /// Creates a new subscriber bound to <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">Bounded channel the broadcaster writes
    /// snapshots into and the subscription's reader loop drains.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channel"/> is <see langword="null"/>.
    /// </exception>
    public ConfigSubscriber(Channel<JsonConfigSnapshot> channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        Channel = channel;
    }

    /// <summary>
    /// Bounded channel the broadcaster writes snapshots into and the
    /// subscription's reader loop drains.
    /// </summary>
    public Channel<JsonConfigSnapshot> Channel { get; }

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
