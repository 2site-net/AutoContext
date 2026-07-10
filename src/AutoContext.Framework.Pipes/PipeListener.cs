namespace AutoContext.Framework.Pipes;

using System.IO.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Layer-3 server-side pipe primitive (unbound state). Holds the
/// configuration needed to claim a named-pipe address and produces a
/// <see cref="BoundPipeListener"/> via <see cref="Bind"/>. The OS
/// resource (pipe instance) is created during <see cref="Bind"/>; if
/// the address is unavailable the failure surfaces there.
/// </summary>
/// <remarks>
/// <para>
/// Type-state design: an unbound <see cref="PipeListener"/> has no
/// pipe instance, so it has no <see cref="IAsyncDisposable"/>; only
/// the <see cref="BoundPipeListener"/> owns OS resources. This rules
/// out "RunAsync before Bind" at compile time.
/// </para>
/// <para>
/// Ready-marker contract: callers that emit a stderr ready marker
/// (e.g. the worker dispatcher) call <see cref="Bind"/>, write the
/// marker, then call <see cref="BoundPipeListener.RunAsync"/>. After
/// <see cref="Bind"/> returns the pipe is listening and clients can
/// queue connections, so the marker is observably truthful.
/// </para>
/// </remarks>
public sealed class PipeListener
{
    private readonly string _pipeName;
    private readonly int _maxInstances;
    private readonly ILogger<PipeListener> _logger;
    private int _bound;

    /// <summary>
    /// Creates a new <see cref="PipeListener"/>.
    /// </summary>
    /// <param name="pipeName">Pipe name (without the
    /// <c>\\.\pipe\</c> prefix on Windows). Must be non-empty.</param>
    /// <param name="maxInstances">Maximum concurrent server
    /// instances; defaults to
    /// <see cref="NamedPipeServerStream.MaxAllowedServerInstances"/>
    /// (unlimited).</param>
    /// <param name="logger">Required logger.</param>
    public PipeListener(
        string pipeName,
        ILogger<PipeListener> logger,
        int maxInstances = NamedPipeServerStream.MaxAllowedServerInstances)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(logger);
        if (maxInstances is 0 or (< 0 and not NamedPipeServerStream.MaxAllowedServerInstances))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxInstances),
                maxInstances,
                "maxInstances must be a positive value or NamedPipeServerStream.MaxAllowedServerInstances.");
        }

        _pipeName = pipeName;
        _maxInstances = maxInstances;
        _logger = logger;
    }

    /// <summary>
    /// Claims the pipe address by constructing the first
    /// <see cref="NamedPipeServerStream"/>. One-shot — subsequent
    /// calls throw. The initial instance is created with
    /// <see cref="PipeOptions.FirstPipeInstance"/>, so if another
    /// process already owns this pipe name the bind fails fast
    /// (the OS denies the duplicate first-instance claim) instead of
    /// silently adding a second server instance — which would split
    /// client connections across two owners. The accept loop's
    /// replenishment instances in <see cref="BoundPipeListener"/>
    /// deliberately omit the flag — they are additional instances of
    /// a name this process already owns.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Bind"/> has already been invoked on this listener.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Another process
    /// already owns this pipe name (the first-instance claim is
    /// denied), or the current principal lacks permission to create
    /// the pipe — the OS reports both as access-denied.</exception>
    /// <exception cref="IOException">The OS otherwise rejected the
    /// bind.</exception>
    public BoundPipeListener Bind()
    {
        if (Interlocked.Exchange(ref _bound, 1) != 0)
        {
            throw new InvalidOperationException(
                $"Pipe listener for '{_pipeName}' has already been bound.");
        }

        // FirstPipeInstance makes this the exclusive owner of the name:
        // a second process attempting the same bind fails rather than
        // becoming a rival server instance the OS would round-robin
        // client connects across.
        var pipe = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            _maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance);

        return new BoundPipeListener(_pipeName, _maxInstances, pipe, _logger);
    }
}
