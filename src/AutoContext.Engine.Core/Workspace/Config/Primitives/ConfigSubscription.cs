namespace AutoContext.Engine.Core.Workspace.Config.Primitives;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Config;

/// <summary>
/// Handle returned from
/// <see cref="ConfigSubscriptionBroadcaster.Subscribe"/>. Drains
/// <see cref="JsonConfigStreamFrame"/> values via
/// <see cref="ReadAllAsync"/> and releases the subscription on
/// <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// Each drained <see cref="JsonConfigSnapshot"/> is yielded as a
/// <see cref="JsonConfigSnapshotFrame"/>; if the broadcaster evicted
/// the subscriber for slowness, a terminal
/// <see cref="JsonConfigEvictedFrame"/> with reason
/// <see cref="JsonConfigEvictedFrame.SlowSubscriberReason"/> is
/// yielded after the underlying channel completes so the stream
/// pump can flush it to the wire before closing the connection.
/// </remarks>
internal sealed class ConfigSubscription : IDisposable
{
    private int _disposed;
    private readonly ChannelReader<JsonConfigSnapshot> _reader;
    private readonly Action _release;
    private readonly Func<bool> _wasEvicted;

    /// <summary>
    /// Creates a new <see cref="ConfigSubscription"/>.
    /// </summary>
    /// <param name="reader">Reader half of the per-subscriber
    /// bounded channel the broadcaster fans snapshots into.</param>
    /// <param name="release">Callback invoked exactly once on
    /// <see cref="Dispose"/> to unsubscribe from the
    /// broadcaster and complete the underlying channel.</param>
    /// <param name="wasEvicted">Probe consulted after the channel
    /// completes to decide whether a terminal
    /// <see cref="JsonConfigEvictedFrame"/> is yielded; closes over
    /// the owning <see cref="ConfigSubscriber"/>'s state.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public ConfigSubscription(
        ChannelReader<JsonConfigSnapshot> reader,
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
    /// Drains broadcaster frames until the channel completes or
    /// <paramref name="cancellationToken"/> fires. If the subscriber
    /// was evicted for slowness, yields a final
    /// <see cref="JsonConfigEvictedFrame"/> after the channel
    /// completes so the caller can flush it to the wire before
    /// closing.
    /// </summary>
    public async IAsyncEnumerable<JsonConfigStreamFrame> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var snapshot in _reader
            .ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new JsonConfigSnapshotFrame(snapshot);
        }

        if (_wasEvicted())
        {
            yield return new JsonConfigEvictedFrame(JsonConfigEvictedFrame.SlowSubscriberReason);
        }
    }
}
