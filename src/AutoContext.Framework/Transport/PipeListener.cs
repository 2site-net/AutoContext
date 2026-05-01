namespace AutoContext.Framework.Transport;

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
    /// calls throw.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Bind"/> has already been invoked on this listener.
    /// </exception>
    /// <exception cref="IOException">The pipe address is already in
    /// use or the OS rejected the bind.</exception>
    /// <exception cref="UnauthorizedAccessException">The current
    /// principal lacks permission to create the pipe.</exception>
    public BoundPipeListener Bind()
    {
        if (Interlocked.Exchange(ref _bound, 1) != 0)
        {
            throw new InvalidOperationException(
                $"Pipe listener for '{_pipeName}' has already been bound.");
        }

        var pipe = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            _maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        return new BoundPipeListener(_pipeName, _maxInstances, pipe, _logger);
    }
}
