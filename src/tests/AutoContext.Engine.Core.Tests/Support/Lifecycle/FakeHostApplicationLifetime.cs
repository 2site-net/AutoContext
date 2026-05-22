namespace AutoContext.Engine.Core.Tests.Support.Lifecycle;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Minimal <see cref="IHostApplicationLifetime"/> fake used by
/// LifecycleService / DispatchPolicy tests. Records how many times
/// <see cref="StopApplication"/> was called and signals the
/// <see cref="ApplicationStopping"/> token on the first call.
/// </summary>
internal sealed class FakeHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource _startedCts = new();
    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly CancellationTokenSource _stoppedCts = new();
    private readonly TaskCompletionSource _stopRequestedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _stopRequested;

    public CancellationToken ApplicationStarted => _startedCts.Token;

    public CancellationToken ApplicationStopping => _stoppingCts.Token;

    public CancellationToken ApplicationStopped => _stoppedCts.Token;

    public int StopApplicationCallCount => Volatile.Read(ref _stopRequested);

    /// <summary>
    /// Completes the first time <see cref="StopApplication"/> is
    /// invoked. Tests await this instead of polling
    /// <see cref="StopApplicationCallCount"/>.
    /// </summary>
    public Task StopApplicationRequested => _stopRequestedTcs.Task;

    public void StopApplication()
    {
        if (Interlocked.Increment(ref _stopRequested) == 1)
        {
            _stoppingCts.Cancel();
            _stopRequestedTcs.TrySetResult();
        }
    }

    /// <summary>
    /// Waits up to <paramref name="budget"/> for the first
    /// <see cref="StopApplication"/> call. Returns silently when
    /// the budget elapses without a call — callers then assert on
    /// <see cref="StopApplicationCallCount"/> to distinguish the
    /// fired/did-not-fire outcomes with a precise failure message.
    /// </summary>
    public async Task WaitForStopRequestedAsync(TimeSpan budget)
    {
        try
        {
            await StopApplicationRequested.WaitAsync(budget).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    public void Dispose()
    {
        _startedCts.Dispose();
        _stoppingCts.Dispose();
        _stoppedCts.Dispose();
    }
}
