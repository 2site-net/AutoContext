namespace AutoContext.Engine.Core.Logging;

/// <summary>
/// Rotation-size selector for the engine's own <c>engine.log</c> and
/// per-worker <c>worker-&lt;workerId&gt;.log</c> files. Matches the
/// <c>--log-rotation</c> CLI switch per
/// <c>design § Engine options &gt; --log-rotation</c>.
/// </summary>
/// <remarks>
/// The selector governs only the rotation thresholds (line count and
/// byte size at which the active file is renamed aside and a fresh one
/// opened); it does <b>not</b> change which records are emitted.
/// Log-level filtering remains an in-process configuration concern.
/// </remarks>
public enum LogRotationSize
{
    /// <summary>
    /// Default rotation size. Rotates at 1,000 lines or 5 MB,
    /// whichever is hit first.
    /// </summary>
    Small = 0,

    /// <summary>
    /// Large rotation size, sized for a session running at a lowered
    /// log level. Rotates at 5,000 lines or 25 MB, whichever is hit
    /// first.
    /// </summary>
    Large = 1,
}
