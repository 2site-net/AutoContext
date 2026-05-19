namespace AutoContext.Engine.Core.Tests.Testing.Fakes;

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

    public void Dispose()
    {
        _startedCts.Dispose();
        _stoppingCts.Dispose();
        _stoppedCts.Dispose();
    }
}
