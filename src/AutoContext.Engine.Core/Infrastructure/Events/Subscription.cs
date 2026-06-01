namespace AutoContext.Engine.Core.Infrastructure.Events;

using System.Threading.Channels;

/// <summary>
/// Base handle returned from a
/// <see cref="SubscriptionBroadcaster{TPayload, TSubscription}"/>'s
/// <c>Subscribe</c>. Owns the disposal and drain plumbing shared by
/// every domain subscription: a one-shot release on
/// <see cref="Dispose"/> and a raw payload drain exposed to derived
/// types via <see cref="ReadPayloadsAsync"/>.
/// </summary>
/// <remarks>
/// Frame mapping stays domain-specific. Each derived subscription
/// drains <see cref="ReadPayloadsAsync"/>, wraps each payload in its
/// own wire DTO, and — when <see cref="WasEvicted"/> reports a
/// slow-subscriber drop — yields its own terminal <c>evicted</c>
/// frame after the channel completes.
/// </remarks>
/// <typeparam name="T">Payload type drained from the channel.</typeparam>
internal abstract class Subscription<T> : IDisposable
{
    private int _disposed;
    private readonly ChannelReader<T> _reader;
    private readonly Action _release;
    private readonly Func<bool> _wasEvicted;

    /// <summary>
    /// Creates a new <see cref="Subscription{T}"/>.
    /// </summary>
    /// <param name="reader">Reader half of the per-subscriber bounded
    /// channel the broadcaster fans payloads into.</param>
    /// <param name="release">Callback invoked exactly once on
    /// <see cref="Dispose"/> to unsubscribe from the broadcaster and
    /// complete the underlying channel.</param>
    /// <param name="wasEvicted">Probe consulted after the channel
    /// completes to decide whether a terminal <c>evicted</c> frame is
    /// yielded; closes over the owning <see cref="Subscriber{T}"/>'s
    /// state.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    protected Subscription(
        ChannelReader<T> reader,
        Action release,
        Func<bool> wasEvicted)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(wasEvicted);

        _reader = reader;
        _release = release;
        _wasEvicted = wasEvicted;
    }

    /// <summary>
    /// <see langword="true"/> if the broadcaster evicted this
    /// subscriber for slowness. Consulted by derived subscriptions
    /// after the drain completes to decide whether to yield a
    /// terminal <c>evicted</c> frame.
    /// </summary>
    protected bool WasEvicted
        => _wasEvicted();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _release();
    }

    /// <summary>
    /// Drains raw payloads until the channel completes or
    /// <paramref name="cancellationToken"/> fires. Derived
    /// subscriptions wrap each payload in their domain wire DTO.
    /// </summary>
    /// <param name="cancellationToken">Cancels the drain.</param>
    /// <returns>The raw payloads fanned in by the broadcaster.</returns>
    protected IAsyncEnumerable<T> ReadPayloadsAsync(
        CancellationToken cancellationToken)
        => _reader.ReadAllAsync(cancellationToken);
}
