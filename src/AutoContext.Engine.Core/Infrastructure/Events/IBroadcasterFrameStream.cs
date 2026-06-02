namespace AutoContext.Engine.Core.Infrastructure.Events;

/// <summary>
/// Transforms a live <see cref="BroadcasterSubscription{T}"/> of
/// <typeparamref name="TPayload"/> into a stream of
/// <typeparamref name="TFrame"/> wire frames. Implementations drain the
/// subscription, map each payload onto a frame, and append a terminal
/// <c>dropped</c> frame when the broadcaster dropped the subscriber for
/// slowness.
/// </summary>
/// <typeparam name="TPayload">Payload fanned in by the broadcaster.</typeparam>
/// <typeparam name="TFrame">Wire frame yielded to the caller.</typeparam>
internal interface IBroadcasterFrameStream<TPayload, TFrame>
    where TPayload : class
{
    /// <summary>
    /// Drains <paramref name="subscription"/> until the channel
    /// completes or <paramref name="cancellationToken"/> fires,
    /// yielding each payload as a <typeparamref name="TFrame"/> and, when
    /// the subscriber was dropped for slowness, a terminal
    /// <c>dropped</c> frame after the channel completes.
    /// </summary>
    /// <param name="subscription">Live subscription to drain.</param>
    /// <param name="cancellationToken">Cancels the drain.</param>
    /// <returns>The framed stream.</returns>
    IAsyncEnumerable<TFrame> StreamAsync(
        BroadcasterSubscription<TPayload> subscription,
        CancellationToken cancellationToken = default);
}
