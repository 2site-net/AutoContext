namespace AutoContext.Engine.Core.Workspace.Config;

using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

/// <summary>
/// Serializes config mutations through a single writer loop and folds
/// the toggles of one logical bulk action into a single on-disk write.
/// </summary>
/// <remarks>
/// A user clicking "disable all instructions in this folder", a script
/// firing several <c>Config.Toggle*</c> calls, or a host hook emitting
/// multiple routing-driven toggles all arrive as N edits within tens of
/// milliseconds. Without coalescing the writer mutex is taken N times,
/// the file is rewritten N times, and the snapshot fans out N times.
/// This writer queues edits, collects every edit that lands inside a
/// short micro-batch window, and applies them through one
/// <see cref="IConfigUpdater.UpdateAsync"/> call — one write,
/// one snapshot swap, one fan-out. Each caller's task completes when the
/// batch that included its edit has been applied.
/// </remarks>
internal sealed class ConfigBatchWriter : IDisposable
{
    private static readonly TimeSpan DefaultBatchWindow = TimeSpan.FromMilliseconds(5);

    private readonly TimeSpan _batchWindow;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private readonly Task _loop;
    private readonly Channel<WriteRequest> _requests = Channel.CreateUnbounded<WriteRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly TimeProvider _timeProvider;
    private readonly IConfigUpdater _updater;

    /// <summary>
    /// Initializes a new <see cref="ConfigBatchWriter"/> over the supplied
    /// updater.
    /// </summary>
    /// <param name="updater">The target whose writes are coalesced.</param>
    /// <param name="timeProvider">
    /// Clock backing the micro-batch window; defaults to
    /// <see cref="TimeProvider.System"/>.
    /// </param>
    /// <param name="batchWindow">
    /// How long to collect further edits before flushing a batch; defaults
    /// to a few milliseconds. Must be positive when supplied.
    /// </param>
    public ConfigBatchWriter(
        IConfigUpdater updater,
        TimeProvider? timeProvider = null,
        TimeSpan? batchWindow = null)
    {
        ArgumentNullException.ThrowIfNull(updater);

        if (batchWindow is { } window)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero, nameof(batchWindow));
        }

        _updater = updater;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _batchWindow = batchWindow ?? DefaultBatchWindow;
        _loop = DrainAsync(_cts.Token);
    }

    /// <summary>
    /// Stops the writer loop and cancels any edits that have not yet been
    /// applied.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _requests.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            _loop.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
    }

    /// <summary>
    /// Queues an edit and returns a task that completes once the batch
    /// containing it has been applied.
    /// </summary>
    /// <param name="edit">The pure transform to fold into the next batch.</param>
    /// <param name="cancellationToken">
    /// Drops the edit from its batch if signalled before the batch is
    /// applied.
    /// </param>
    /// <returns>A task tracking the edit's persistence.</returns>
    public Task EnqueueAsync(
        Func<AutoContextConfig, AutoContextConfig> edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var request = new WriteRequest(edit, cancellationToken);

        if (!_requests.Writer.TryWrite(request))
        {
            request.Completion.TrySetException(new ObjectDisposedException(nameof(ConfigBatchWriter)));
        }

        return request.Completion.Task;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A faulting edit or persist must not tear down the writer loop and strand later callers — the failure is surfaced to that batch's callers through their own task.")]
    private async Task ApplyBatchAsync(List<WriteRequest> batch)
    {
        var active = new List<WriteRequest>(batch.Count);

        foreach (var request in batch)
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                request.Completion.TrySetCanceled(request.CancellationToken);
            }
            else
            {
                active.Add(request);
            }
        }

        if (active.Count == 0)
        {
            return;
        }

        try
        {
            await _updater.UpdateAsync(
                config =>
                {
                    foreach (var request in active)
                    {
                        config = request.Edit(config);
                    }

                    return config;
                },
                _cts.Token)
                .ConfigureAwait(false);

            foreach (var request in active)
            {
                request.Completion.TrySetResult();
            }
        }
        catch (OperationCanceledException ex)
        {
            foreach (var request in active)
            {
                request.Completion.TrySetCanceled(ex.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            foreach (var request in active)
            {
                request.Completion.TrySetException(ex);
            }
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        var reader = _requests.Reader;

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await ProcessBatchAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        while (reader.TryRead(out var request))
        {
            request.Completion.TrySetCanceled(cancellationToken);
        }
    }

    private async Task ProcessBatchAsync(ChannelReader<WriteRequest> reader, CancellationToken cancellationToken)
    {
        var batch = new List<WriteRequest>();

        while (reader.TryRead(out var request))
        {
            batch.Add(request);
        }

        try
        {
            await Task.Delay(_batchWindow, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            foreach (var request in batch)
            {
                request.Completion.TrySetCanceled(cancellationToken);
            }

            throw;
        }

        while (reader.TryRead(out var request))
        {
            batch.Add(request);
        }

        await ApplyBatchAsync(batch).ConfigureAwait(false);
    }

    private sealed class WriteRequest(Func<AutoContextConfig, AutoContextConfig> edit, CancellationToken cancellationToken)
    {
        /// <summary>Gets the token that drops this edit from its batch.</summary>
        public CancellationToken CancellationToken { get; } = cancellationToken;

        /// <summary>Gets the source completed once the edit is applied.</summary>
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the pure transform folded into the batch.</summary>
        public Func<AutoContextConfig, AutoContextConfig> Edit { get; } = edit;
    }
}
