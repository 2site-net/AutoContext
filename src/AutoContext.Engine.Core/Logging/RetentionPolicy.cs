namespace AutoContext.Engine.Core.Logging;

using Microsoft.Extensions.Options;

/// <summary>
/// Single reader of <see cref="EngineOptions.Retention"/>.
/// Both the engine's rotated-log cleaner
/// (<see cref="RotatedLogCleaner"/>) and the cross-instance
/// subtree cleaner (<c>StaleSubtreeCleaner</c>) consult this type
/// instead of reading <c>EngineOptions.Retention</c> directly,
/// so there is exactly one place that interprets the
/// <c>--retention</c> switch — including the
/// <see cref="TimeSpan.Zero"/> sentinel that disables retention
/// entirely.
/// </summary>
/// <remarks>
/// <para>
/// The clock is supplied via <see cref="TimeProvider"/> so tests
/// can freeze "now" without sleeping; production registers
/// <see cref="TimeProvider.System"/> via
/// <c>EngineHostBuilderExtensions.AddAutoContextEngine</c>.
/// </para>
/// <para>
/// Future-dated timestamps (clock skew across hosts that share a
/// cache root) are never reported as expired — the comparison is
/// the elapsed-since-now sign, not an absolute cutoff that could
/// flip negative.
/// </para>
/// </remarks>
internal sealed class RetentionPolicy
{
    private readonly IOptions<EngineOptions> _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new <see cref="RetentionPolicy"/> bound to the
    /// host's options pipeline and clock.
    /// </summary>
    /// <param name="options">Options accessor for the engine's
    /// <see cref="EngineOptions.Retention"/> window.</param>
    /// <param name="timeProvider">Clock source.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public RetentionPolicy(
        IOptions<EngineOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// The configured retention window.
    /// <see cref="TimeSpan.Zero"/> means "expire immediately" —
    /// every artefact older than the moment of the check is
    /// reaped on the next sweep.
    /// </summary>
    public TimeSpan Window
        => _options.Value.Retention;

    /// <summary>
    /// Returns <see langword="true"/> when an artefact stamped at
    /// <paramref name="writtenAt"/> falls outside the configured
    /// retention window relative to the current clock.
    /// </summary>
    /// <param name="writtenAt">When the artefact was produced —
    /// for rotated logs this is the timestamp embedded in the
    /// filename per
    /// <c>design § Housekeeping &gt; Log rotation</c>.</param>
    /// <returns><see langword="true"/> if the artefact is older
    /// than <see cref="Window"/>; <see langword="false"/>
    /// otherwise. A <see cref="Window"/> of
    /// <see cref="TimeSpan.Zero"/> always returns
    /// <see langword="true"/>; a future-dated
    /// <paramref name="writtenAt"/> always returns
    /// <see langword="false"/>.</returns>
    public bool IsExpired(DateTimeOffset writtenAt)
        => IsExpired(writtenAt, Window);

    /// <summary>
    /// Returns <see langword="true"/> when an artefact stamped at
    /// <paramref name="writtenAt"/> falls outside the explicit
    /// <paramref name="window"/> relative to the current clock.
    /// Lets callers honour per-entry retention windows
    /// (<see cref="Protocol.Messages.Registry.JsonRegistryEntry.Retention"/>)
    /// without re-implementing the future-skew and zero-window
    /// rules — <c>StaleSubtreeCleaner</c> uses this overload for the
    /// <c>StaleRegistration</c> arm.
    /// </summary>
    /// <param name="writtenAt">When the artefact was produced.</param>
    /// <param name="window">Retention window to apply.
    /// <see cref="TimeSpan.Zero"/> means "expire immediately".</param>
    /// <returns><see langword="true"/> if the artefact is older
    /// than <paramref name="window"/>; <see langword="false"/>
    /// otherwise. A <paramref name="window"/> of
    /// <see cref="TimeSpan.Zero"/> always returns
    /// <see langword="true"/>; a future-dated
    /// <paramref name="writtenAt"/> always returns
    /// <see langword="false"/>.</returns>
    public bool IsExpired(DateTimeOffset writtenAt, TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            return true;
        }

        var elapsed = _timeProvider.GetUtcNow() - writtenAt;
        return elapsed > window;
    }
}
