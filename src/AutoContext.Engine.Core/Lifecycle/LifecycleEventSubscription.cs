namespace AutoContext.Engine.Core.Lifecycle;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// Handle returned from <see cref="LifecycleEventStream.Subscribe"/>.
/// Drains <see cref="LifecycleEvent"/> values via
/// <see cref="ReadAllAsync"/> and releases the subscription on
/// <see cref="Dispose"/>.
/// </summary>
internal sealed class LifecycleEventSubscription : IDisposable
{
    private int _disposed;
    private readonly ChannelReader<LifecycleEvent> _reader;
    private readonly Action _release;
    private readonly Func<bool> _wasEvicted;

    public LifecycleEventSubscription(
        ChannelReader<LifecycleEvent> reader,
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
    /// Drains lifecycle events until the channel completes or
    /// <paramref name="cancellationToken"/> fires. If the subscriber
    /// was evicted for slowness, yields a final
    /// <see cref="LifecycleEventKinds.Evicted"/> frame so the caller
    /// can flush it to the wire before closing.
    /// </summary>
    public async IAsyncEnumerable<LifecycleEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in _reader
            .ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return evt;
        }

        if (_wasEvicted())
        {
            yield return new LifecycleEvent
            {
                Kind = LifecycleEventKinds.Evicted,
                Reason = "slow-subscriber",
            };
        }
    }
}
