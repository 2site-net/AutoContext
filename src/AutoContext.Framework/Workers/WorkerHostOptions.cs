namespace AutoContext.Framework.Workers;

/// <summary>
/// Strongly-typed options for the shared worker host: the named pipe to
/// listen on and the stderr ready-marker emitted once the pipe is accepting
/// connections.
/// </summary>
/// <remarks>
/// Each <c>AutoContext.Worker.*</c> process binds and registers an instance
/// of this type. <see cref="Pipe"/> normally comes from the <c>--pipe</c>
/// command-line argument. <see cref="ReadyMarker"/> is supplied by the
/// process itself (e.g. <c>"[AutoContext.Worker.DotNet] Ready."</c>) and is
/// scraped from the worker's stderr by the parent process to detect that
/// the pipe is up.
/// </remarks>
public sealed class WorkerHostOptions
{
    /// <summary>
    /// Named pipe the worker listens on for per-task requests.
    /// </summary>
    public string Pipe { get; init; } = string.Empty;

    /// <summary>
    /// Exact text written to stderr once the pipe server is accepting
    /// connections. Used by parent processes as a readiness handshake.
    /// </summary>
    public string ReadyMarker { get; init; } = string.Empty;

    /// <summary>
    /// Optional named pipe the worker connects to for streaming structured
    /// log records (NDJSON) to the parent process. When empty (the worker
    /// is launched standalone, or the parent did not pass <c>--log-pipe</c>),
    /// log output falls back to stderr.
    /// </summary>
    public string LogPipe { get; init; } = string.Empty;

    /// <summary>
    /// Optional named pipe the worker connects to on startup to announce
    /// its presence to the extension-side <c>HealthMonitorServer</c>. The
    /// connection is held open for the lifetime of the worker process and
    /// the server uses its closure as the worker's exit signal. When empty
    /// (standalone runs, or the parent did not pass <c>--health-monitor</c>)
    /// no liveness signal is sent.
    /// </summary>
    public string HealthMonitor { get; init; } = string.Empty;
}
