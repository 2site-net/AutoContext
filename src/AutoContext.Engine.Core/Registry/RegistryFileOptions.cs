namespace AutoContext.Engine.Core.Registry;

/// <summary>
/// Tunable knobs shared by <see cref="RegistryFileReader"/> and
/// <see cref="RegistryFileWriter"/> for their exponential-backoff
/// retry loops. Defaults reflect the discipline called out in
/// <c>design § P9</c> and
/// <c>design § engine-registry.json entry lifecycle</c>:
/// <c>FileShare.None</c> (writer) or <c>FileShare.ReadWrite</c>
/// (reader) plus exponential backoff so concurrent engines
/// serialise on the OS file lock without either one corrupting
/// the registry.
/// </summary>
/// <remarks>
/// Tests bypass the production defaults to keep the suite fast.
/// Production callers should accept the defaults; the design does
/// not yet expose any of these knobs on the engine CLI.
/// </remarks>
public sealed class RegistryFileOptions
{
    /// <summary>
    /// Initial back-off applied after the first failed open
    /// attempt. Each subsequent attempt doubles the delay up to
    /// <see cref="MaxRetryDelay"/>.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Cap on the per-attempt back-off. Prevents the doubling
    /// schedule from inflating into multi-second waits when the
    /// peer holding the lock is genuinely stuck.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum number of open attempts before the operation
    /// surfaces the underlying <see cref="IOException"/> to the
    /// caller. The total wall time before failure is bounded by
    /// the geometric series of doubled <see cref="InitialRetryDelay"/>
    /// values clamped to <see cref="MaxRetryDelay"/>. With the
    /// production defaults the worst-case wait before failure is
    /// roughly ten seconds, keeping a contended startup path from
    /// blocking indefinitely while still tolerating a peer that
    /// holds the lock for a few hundred milliseconds.
    /// </summary>
    public int MaxAttempts { get; set; } = 25;

    /// <summary>
    /// Validates the option values are internally consistent.
    /// Called by the <see cref="RegistryFileReader"/> and
    /// <see cref="RegistryFileWriter"/> constructors; callers
    /// rarely need to invoke it directly.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A delay is
    /// non-positive, <see cref="MaxRetryDelay"/> is smaller than
    /// <see cref="InitialRetryDelay"/>, or <see cref="MaxAttempts"/>
    /// is non-positive.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(InitialRetryDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxRetryDelay, InitialRetryDelay);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaxAttempts, 0);
    }
}
