namespace AutoContext.Engine.Core.Registry;

/// <summary>
/// Tunable knobs for <see cref="RegistryFileReader"/>'s
/// exponential-backoff retry loop. The reader opens the file with
/// <see cref="FileShare.ReadWrite"/> | <see cref="FileShare.Delete"/>,
/// so contention is bounded to the brief window when a writer
/// holds the file with <see cref="FileShare.None"/> between
/// truncate and rename. Defaults give roughly a ten-second worst
/// case before the reader surfaces an <see cref="IOException"/>.
/// </summary>
/// <remarks>
/// Tests bypass the production defaults to keep the suite fast.
/// Production callers should accept the defaults; the design does
/// not yet expose any of these knobs on the engine CLI.
/// </remarks>
public sealed class RegistryFileReaderOptions
{
    /// <summary>
    /// Initial back-off applied after the first failed open
    /// attempt. Each subsequent attempt doubles the delay up to
    /// <see cref="MaxRetryDelay"/>.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Cap on the per-attempt back-off.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum number of open attempts before the operation
    /// surfaces the underlying <see cref="IOException"/> to the
    /// caller.
    /// </summary>
    public int MaxAttempts { get; set; } = 25;

    /// <summary>
    /// Validates the option values are internally consistent.
    /// Called by the <see cref="RegistryFileReader"/> constructor;
    /// callers rarely need to invoke it directly.
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
