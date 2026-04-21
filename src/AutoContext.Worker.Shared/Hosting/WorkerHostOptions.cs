namespace AutoContext.Worker.Hosting;

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
}
