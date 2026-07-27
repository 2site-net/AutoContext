namespace AutoContext.Engine.Core.Logging;

/// <summary>
/// Per-rotation-size thresholds for the engine's own
/// <c>engine.log</c> (and, in future, the per-worker
/// <c>worker-&lt;workerId&gt;.log</c> files). Mirrors the table in
/// <c>design § Housekeeping &gt; Log rotation</c>: the active log
/// file is rotated the first time either the line count or the
/// byte size of the file crosses the corresponding threshold.
/// </summary>
/// <param name="MaxLines">Inclusive line-count ceiling. Rotation
/// fires when the active file has reached this many lines.</param>
/// <param name="MaxBytes">Inclusive byte-size ceiling. Rotation
/// fires when the active file has reached this many bytes.</param>
/// <remarks>
/// Test fixtures pass a custom <see cref="LogRotationThresholds"/>
/// directly to <see cref="LogFileSinkService"/> so they can drive
/// rotation with a handful of records instead of having to write
/// thousands of lines. Production callers compose through
/// <see cref="ForRotationSize(LogRotationSize)"/>, which maps
/// <see cref="EngineOptions.LogRotation"/> onto the table above.
/// </remarks>
internal sealed record LogRotationThresholds(int MaxLines, long MaxBytes)
{
    /// <summary>
    /// Resolves the rotation thresholds for
    /// <paramref name="rotationSize"/> per the table in
    /// <c>design § Housekeeping &gt; Log rotation</c>:
    /// <list type="bullet">
    ///   <item><see cref="LogRotationSize.Small"/> —
    ///     1,000 lines or 5 MB.</item>
    ///   <item><see cref="LogRotationSize.Large"/> —
    ///     5,000 lines or 25 MB.</item>
    /// </list>
    /// </summary>
    /// <param name="rotationSize">Rotation-size selector — typically
    /// <see cref="EngineOptions.LogRotation"/>.</param>
    /// <returns>The matching thresholds; unknown enum values fall
    /// back to the <see cref="LogRotationSize.Small"/>
    /// row (defensive default for forward-compatibility, never
    /// reached under the validator's enum-range guard).</returns>
    public static LogRotationThresholds ForRotationSize(LogRotationSize rotationSize)
        => rotationSize switch
        {
            LogRotationSize.Large => new LogRotationThresholds(
                MaxLines: 5_000,
                MaxBytes: 25L * 1024 * 1024),
            LogRotationSize.Small => new LogRotationThresholds(
                MaxLines: 1_000,
                MaxBytes: 5L * 1024 * 1024),
            _ => new LogRotationThresholds(
                MaxLines: 1_000,
                MaxBytes: 5L * 1024 * 1024),
        };
}
