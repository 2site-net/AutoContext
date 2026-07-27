namespace AutoContext.Client.Core.Engine;

/// <summary>
/// Timing budget for the cold-start find-or-spawn flow. Separates the
/// sub-second <em>warm</em> attempt (dial an engine that is expected
/// to already be up) from the multi-second <em>cold</em> window (dial
/// with exponential backoff after spawning one), matching the two
/// budgets the design prescribes.
/// </summary>
public sealed record EngineConnectBudget
{
    /// <summary>The default budget used when a host does not register
    /// its own.</summary>
    public static EngineConnectBudget Default { get; } = new();

    /// <summary>Per-attempt connect timeout for the single warm try,
    /// before any spawn. Kept short so a genuinely absent engine falls
    /// through to spawn quickly.</summary>
    public TimeSpan WarmConnectTimeout { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Total time the resolver keeps retrying after a spawn
    /// before giving up. A self-contained .NET engine binding four
    /// pipes routinely takes hundreds of milliseconds on first
    /// launch.</summary>
    public TimeSpan ColdConnectBudget { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Per-attempt connect timeout during the cold retry
    /// loop.</summary>
    public TimeSpan ColdConnectAttemptTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Delay before the first cold retry.</summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Upper bound on the backoff delay between cold
    /// retries.</summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Multiplier applied to the previous delay each retry.</summary>
    public double RetryDelayMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Returns the next backoff delay given the
    /// <paramref name="previousDelay"/>, capped at
    /// <see cref="MaxRetryDelay"/>. A non-positive previous delay
    /// yields <see cref="InitialRetryDelay"/>.
    /// </summary>
    public TimeSpan NextRetryDelay(TimeSpan previousDelay)
    {
        if (previousDelay <= TimeSpan.Zero)
        {
            return InitialRetryDelay;
        }

        var scaled = TimeSpan.FromTicks((long)(previousDelay.Ticks * RetryDelayMultiplier));
        return scaled > MaxRetryDelay ? MaxRetryDelay : scaled;
    }
}
