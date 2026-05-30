namespace AutoContext.Engine.Core.Lifecycle;

using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// Internal per-subscriber state: the bounded channel events are
/// pushed into, plus an atomic state machine the subscription's
/// reader-side adapter consults to surface the terminal
/// <c>evicted</c> frame.
/// </summary>
internal sealed class LifecycleEventSubscriber
{
    private const int Active = 0;
    private const int Closed = 1;
    private const int Evicted = 2;

    private int _state = Active;

    /// <summary>
    /// Creates a new subscriber bound to <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">Bounded channel the stream writes
    /// events into and the subscription's reader loop drains.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channel"/> is <see langword="null"/>.
    /// </exception>
    public LifecycleEventSubscriber(Channel<JsonLifecycleEvent> channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        Channel = channel;
    }

    /// <summary>
    /// Bounded channel the stream writes events into and the
    /// subscription's reader loop drains.
    /// </summary>
    public Channel<JsonLifecycleEvent> Channel { get; }

    /// <summary>
    /// <see langword="true"/> once the stream has dropped this
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
