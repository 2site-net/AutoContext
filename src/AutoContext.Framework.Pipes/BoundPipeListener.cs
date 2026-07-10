namespace AutoContext.Framework.Pipes;

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Layer-3 server-side pipe primitive (bound state). Owns the
/// pipe-name OS resource and runs an accept loop, dispatching each
/// accepted connection to the caller-supplied handler. Only
/// producible via <see cref="PipeListener.Bind"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each accepted <see cref="Stream"/> is owned by the listener and
/// disposed after the handler returns; handlers should not dispose it
/// themselves. The listener creates a fresh
/// <see cref="NamedPipeServerStream"/> for each accept so multi-client
/// peers can connect concurrently up to the configured instance
/// limit.
/// </para>
/// <para>
/// Several accepts are kept in flight at once (a small backlog) so at
/// least one server instance is always listening, even while another
/// instance is being re-armed after a connection. This keeps the pipe
/// path continuously serviceable regardless of how the accept loop's
/// thread is scheduled, which matters under heavy load when many
/// processes are spawned concurrently.
/// </para>
/// <para>
/// <see cref="RunAsync"/> is one-shot. <see cref="DisposeAsync"/> is
/// the canonical teardown and may be called whether or not
/// <see cref="RunAsync"/> ran.
/// </para>
/// </remarks>
public sealed partial class BoundPipeListener : IAsyncDisposable
{
    /// <summary>
    /// Number of overlapping accepts to keep pre-armed and listening.
    /// Two is the minimum that survives a single re-arm (while one
    /// accepted instance is being replaced another is still listening),
    /// but the re-arm is reactive — a thread-pool continuation scheduled
    /// after a connection is accepted — so under CPU starvation it can
    /// lag behind a burst of rapid sequential connects, draining the pool
    /// to zero and making a client see <c>ERROR_PIPE_BUSY</c>. A deeper
    /// backlog keeps enough instances pre-armed that a realistic connect
    /// burst is served entirely from the pool even if every re-arm is
    /// delayed.
    /// </summary>
    private const int DefaultAcceptBacklog = 4;

    private readonly string _pipeName;
    private readonly int _maxInstances;
    private readonly int _acceptBacklog;
    private readonly ILogger<PipeListener> _logger;
    private NamedPipeServerStream? _initialPipe;
    private int _running;
    private int _disposed;

    internal BoundPipeListener(
        string pipeName,
        int maxInstances,
        NamedPipeServerStream initialPipe,
        ILogger<PipeListener> logger)
    {
        _pipeName = pipeName;
        _maxInstances = maxInstances;
        _acceptBacklog = maxInstances == NamedPipeServerStream.MaxAllowedServerInstances
            ? DefaultAcceptBacklog
            : Math.Min(maxInstances, DefaultAcceptBacklog);
        _initialPipe = initialPipe;
        _logger = logger;
    }

    /// <summary>
    /// Runs the accept loop. Returns only after the loop stops AND
    /// every in-flight connection handler has finished.
    /// </summary>
    /// <param name="connectionHandler">
    /// Invoked once per accepted connection with the connected
    /// <see cref="Stream"/> and the listener's cancellation token.
    /// The listener disposes the stream after the handler returns;
    /// the handler must not dispose it.
    /// </param>
    /// <param name="cancellationToken">Cooperative cancellation. The
    /// accept loop exits cleanly when canceled and the method
    /// completes once outstanding handlers have drained.</param>
    /// <exception cref="InvalidOperationException">
    /// <see cref="RunAsync"/> has already been invoked.
    /// </exception>
    public async Task RunAsync(
        Func<Stream, CancellationToken, Task> connectionHandler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connectionHandler);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            throw new InvalidOperationException(
                $"Pipe listener for '{_pipeName}' has already been run.");
        }

        var connections = new List<Task>();
        var accepts = new List<Task<NamedPipeServerStream?>>(_acceptBacklog);

        // A linked source lets the finally block unblock accepts that are
        // still waiting for a connection — for example if one accept
        // faulted unexpectedly and left its siblings pending — so every
        // server stream is disposed before RunAsync returns. Canceling it
        // does not affect the caller's token, so connection handlers keep
        // observing the original token.
        using var acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var acceptToken = acceptCts.Token;
        try
        {
            // Start a backlog of overlapping accepts. Keeping more than
            // one accept in flight means a server instance is always
            // listening while another is being re-armed after a
            // connection. A single-instance loop instead leaves a window
            // with zero listening instances between accepting a
            // connection and creating the next one; under heavy CPU load
            // (many concurrently spawned processes) the accept-loop
            // thread can be starved long enough for that window to exceed
            // a client's connect timeout.
            for (var i = 0; i < _acceptBacklog && !acceptToken.IsCancellationRequested; i++)
            {
                accepts.Add(AcceptAsync(acceptToken));
            }

            while (accepts.Count > 0)
            {
                var completed = await Task.WhenAny(accepts).ConfigureAwait(false);
                accepts.Remove(completed);

                var pipe = await completed.ConfigureAwait(false);
                if (pipe is null)
                {
                    // Canceled or faulted into shutdown: stop
                    // replenishing and let the remaining accepts drain.
                    continue;
                }

                // Re-arm the replacement instance BEFORE dispatching the
                // handler so the listening pool is topped up before any
                // per-connection work runs — the accept path never waits
                // on handler progress.
                if (!acceptToken.IsCancellationRequested)
                {
                    accepts.Add(AcceptAsync(acceptToken));
                }

                connections.Add(InvokeHandlerAsync(pipe, connectionHandler, cancellationToken));
            }
        }
        finally
        {
            // Unblock and drain any accepts still waiting for a connection
            // (left pending if the loop exited via an unexpected fault),
            // disposing their server streams, then wait for in-flight
            // handlers to finish.
            await acceptCts.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(accepts).ConfigureAwait(false);
            await Task.WhenAll(connections).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Disposes the Bind-created instance if RunAsync never claimed
        // it (RunAsync not started, or it returned before the first
        // accept). Once RunAsync starts, each accept owns and disposes
        // its own instance on cancellation.
        var pending = Interlocked.Exchange(ref _initialPipe, null);
        if (pending is not null)
        {
            await pending.DisposeAsync().ConfigureAwait(false);
        }
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership transfers to the caller on success via `ownsPipe = false`; the finally block disposes on every other path.")]
    private async Task<NamedPipeServerStream?> AcceptAsync(CancellationToken cancellationToken)
    {
        // The first accept claims the instance created by Bind; every
        // other accept (backlog accepts and post-connection
        // replenishment) creates its own fresh server stream. The
        // Interlocked exchange makes the hand-off race-free when several
        // accepts start together.
        var pipe = Interlocked.Exchange(ref _initialPipe, null) ?? CreateServerStream();
        var ownsPipe = true;
        CancellationTokenRegistration registration = default;

        try
        {
            // On Windows, WaitForConnectionAsync does not reliably
            // honor the cancellation token. Disposing the pipe from
            // the cancellation callback forces the wait to throw.
            registration = cancellationToken.Register(pipe.Dispose);

            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

            ownsPipe = false;
            return pipe;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);

            if (ownsPipe)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    [SuppressMessage("Design", "CA1031",
        Justification = "Listener boundary: handler failures are logged and isolated so one bad connection cannot crash the accept loop.")]
    private async Task InvokeHandlerAsync(
        NamedPipeServerStream pipe,
        Func<Stream, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        await using (pipe.ConfigureAwait(false))
        {
            try
            {
                await handler(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown — exit silently.
            }
            catch (Exception ex) when (!IsCritical(ex))
            {
                LogHandlerFailed(_logger, _pipeName, ex);
            }
        }
    }

    private NamedPipeServerStream CreateServerStream() =>
        new(
            _pipeName,
            PipeDirection.InOut,
            _maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

    /// <summary>
    /// Critical exceptions that indicate the process is in an
    /// unrecoverable state. They escape the per-handler catch-all so
    /// the host can fail fast.
    /// </summary>
    private static bool IsCritical(Exception ex) =>
        ex is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or ThreadAbortException;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Pipe listener '{PipeName}' connection handler threw an unhandled exception.")]
    private static partial void LogHandlerFailed(ILogger logger, string pipeName, Exception exception);
}
