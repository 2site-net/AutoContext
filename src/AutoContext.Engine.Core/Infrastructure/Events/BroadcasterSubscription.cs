namespace AutoContext.Engine.Core.Infrastructure.Events;

using System.Threading.Channels;

/// <summary>
/// Handle returned from a <see cref="Broadcaster{T}"/>'s
/// <c>Subscribe</c>. Owns the disposal and drain plumbing shared by
/// every stream: a one-shot release on <see cref="Dispose"/>, a raw
/// payload drain via <see cref="ReadAllAsync"/>, and the
/// <see cref="WasDropped"/> probe.
/// </summary>
/// <remarks>
/// Frame mapping is a caller concern: a domain framing function drains
/// <see cref="ReadAllAsync"/>, wraps each payload in its own wire DTO,
/// and — when <see cref="WasDropped"/> reports a slow-subscriber drop —
/// yields its own terminal <c>dropped</c> frame after the channel
/// completes. The write, close, and drop side of a subscriber stays
/// inside the broadcaster and never surfaces here.
/// </remarks>
/// <typeparam name="T">Payload type drained from the channel.</typeparam>
internal sealed class BroadcasterSubscription<T> : IDisposable
{
    private int _disposed;
    private readonly ChannelReader<T> _reader;
    private readonly Action _release;
    private readonly Func<bool> _wasDropped;

    /// <summary>
    /// Creates a new <see cref="BroadcasterSubscription{T}"/>.
    /// </summary>
    /// <param name="reader">Reader half of the per-subscriber bounded
    /// channel the broadcaster fans payloads into.</param>
    /// <param name="release">Callback invoked exactly once on
    /// <see cref="Dispose"/> to unsubscribe from the broadcaster and
    /// complete the underlying channel.</param>
    /// <param name="wasDropped">Probe consulted after the channel
    /// completes to decide whether a terminal <c>dropped</c> frame is
    /// yielded; closes over the owning <see cref="BroadcasterSubscriber{T}"/>'s
    /// state.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    internal BroadcasterSubscription(
        ChannelReader<T> reader,
        Action release,
        Func<bool> wasDropped)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(wasDropped);

        _reader = reader;
        _release = release;
        _wasDropped = wasDropped;
    }

    /// <summary>
    /// <see langword="true"/> if the broadcaster dropped this
    /// subscriber for slowness. Consulted by a framing function after
    /// the drain completes to decide whether to yield a terminal
    /// <c>dropped</c> frame.
    /// </summary>
    public bool WasDropped
        => _wasDropped();

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
    /// <paramref name="cancellationToken"/> fires. A framing function
    /// wraps each payload in its domain wire DTO.
    /// </summary>
    /// <param name="cancellationToken">Cancels the drain.</param>
    /// <returns>The raw payloads fanned in by the broadcaster.</returns>
    public IAsyncEnumerable<T> ReadAllAsync(
        CancellationToken cancellationToken)
        => _reader.ReadAllAsync(cancellationToken);
}
