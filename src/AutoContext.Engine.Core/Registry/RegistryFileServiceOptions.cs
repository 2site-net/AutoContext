namespace AutoContext.Engine.Core.Registry;

/// <summary>
/// Tunable knobs for <see cref="RegistryFileService"/>'s
/// cross-process coordination and lifecycle.
/// </summary>
public sealed class RegistryFileServiceOptions
{
    /// <summary>
    /// Maximum time to wait for the cross-process mutex before
    /// surfacing a <see cref="TimeoutException"/> to the caller.
    /// </summary>
    public TimeSpan MutexAcquireTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum time <see cref="RegistryFileService.StopAsync"/>
    /// waits for the worker thread to drain pending writes before
    /// it cancels in-flight requests and tears down. Pending
    /// requests still in the channel at the cutoff are completed
    /// with <see cref="OperationCanceledException"/>.
    /// </summary>
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Validates the option values are internally consistent.
    /// Called by the <see cref="RegistryFileService"/> constructor.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A timeout is
    /// non-positive.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MutexAcquireTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ShutdownDrainTimeout, TimeSpan.Zero);
    }
}
