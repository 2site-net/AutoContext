namespace AutoContext.Engine.Core.Logging;

/// <summary>
/// Log-rotation verbosity selector for the engine's own
/// <c>engine.log</c> and per-worker <c>worker-&lt;workerId&gt;.log</c>
/// files. Matches the <c>--logging</c> CLI switch per
/// <c>design § Engine options &gt; --logging</c>.
/// </summary>
/// <remarks>
/// The selector governs only the rotation thresholds (line count and
/// byte size at which a new file is started); it does <b>not</b>
/// change which records are emitted. Log-level filtering remains an
/// in-process configuration concern.
/// </remarks>
public enum LogVerbosity
{
    /// <summary>
    /// Default verbosity. Rotates at 1,000 lines or 5 MB, whichever
    /// is hit first.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Debug verbosity. Rotates at 5,000 lines or 25 MB, whichever
    /// is hit first.
    /// </summary>
    Debug = 1,
}
