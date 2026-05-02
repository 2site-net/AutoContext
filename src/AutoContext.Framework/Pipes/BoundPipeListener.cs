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
/// <see cref="RunAsync"/> is one-shot. <see cref="DisposeAsync"/> is
/// the canonical teardown and may be called whether or not
/// <see cref="RunAsync"/> ran.
/// </para>
/// </remarks>
public sealed partial class BoundPipeListener : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly int _maxInstances;
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
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pipe = await AcceptAsync(cancellationToken).ConfigureAwait(false);
                if (pipe is null)
                {
                    break;
                }

                connections.Add(InvokeHandlerAsync(pipe, connectionHandler, cancellationToken));
            }
        }
        finally
        {
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

        var initial = Interlocked.Exchange(ref _initialPipe, null);
        if (initial is not null)
        {
            await initial.DisposeAsync().ConfigureAwait(false);
        }
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership transfers to the caller on success via `ownsPipe = false`; the finally block disposes on every other path.")]
    private async Task<NamedPipeServerStream?> AcceptAsync(CancellationToken cancellationToken)
    {
        // Use the pre-bound instance for connection #1, then create a
        // fresh server stream per accept for subsequent connections.
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
