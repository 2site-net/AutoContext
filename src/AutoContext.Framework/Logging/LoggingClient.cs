namespace AutoContext.Framework.Logging;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

/// <summary>
/// Background pipe-client that drains <see cref="LogEntry"/> values from
/// a bounded channel and writes them as NDJSON over a named pipe to the
/// extension-side LogServer. When the pipe is unavailable (no pipe name
/// supplied, the connect attempt fails, or the pipe subsequently breaks)
/// the client transparently falls back to writing each record to stderr
/// in a human-readable single-line format.
/// </summary>
/// <remarks>
/// The bounded channel uses <see cref="BoundedChannelFullMode.DropOldest"/>
/// — log spam can never block caller code or grow memory unbounded. A
/// single drain task owns all I/O. Failures (broken pipe, serialisation
/// errors) are intentionally swallowed: this type IS the logger of last
/// resort, so it must never throw.
/// </remarks>
public sealed class LoggingClient : IAsyncDisposable
{
    private const int QueueCapacity = 1024;
    private const int ConnectTimeoutMs = 2000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Channel<LogEntry> _queue = Channel.CreateBounded<LogEntry>(
        new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly string _pipeName;
    private readonly string _clientName;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;

    /// <summary>
    /// Creates a new <see cref="LoggingClient"/>. Pass <see langword="null"/>
    /// or empty for <paramref name="pipeName"/> to force stderr fallback
    /// (useful for standalone runs and tests).
    /// </summary>
    public LoggingClient(string? pipeName, string clientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        _pipeName = pipeName ?? string.Empty;
        _clientName = clientName;
        _drainTask = Task.Run(() => DrainAsync(_cts.Token));
    }

    /// <summary>
    /// Posts <paramref name="entry"/> for off-thread delivery. Never
    /// blocks; if the internal buffer is full the oldest entry is dropped.
    /// </summary>
    public void Post(LogEntry entry) => _queue.Writer.TryWrite(entry);

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();

        try
        {
            await _drainTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Drain didn't finish in time — abandon it.
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        // Only dispose the CTS if the drain task has actually exited.
        // Disposing while the task is still mid-await would surface as an
        // ObjectDisposedException on the token — not caught by the drain's
        // OperationCanceledException handler, leading to an unobserved
        // task exception. The CTS is one-per-process; letting the GC
        // reclaim it is harmless.
        if (_drainTask.IsCompleted)
        {
            _cts.Dispose();
        }
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        NamedPipeClientStream? stream = await TryConnectAsync(ct).ConfigureAwait(false);

        try
        {
            if (stream is not null && !await TrySendGreetingAsync(stream, ct).ConfigureAwait(false))
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                stream = null;
            }

            await foreach (var entry in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (stream is not null)
                {
                    if (await TryWritePipeAsync(stream, entry, ct).ConfigureAwait(false))
                    {
                        continue;
                    }

                    await stream.DisposeAsync().ConfigureAwait(false);
                    stream = null;
                }

                WriteStderr(entry);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — fall through.
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<NamedPipeClientStream?> TryConnectAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_pipeName))
        {
            return null;
        }

        var pipe = new NamedPipeClientStream(
            serverName: ".",
            pipeName: _pipeName,
            direction: PipeDirection.Out,
            options: PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(ConnectTimeoutMs, ct).ConfigureAwait(false);
            return pipe;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    private async Task<bool> TrySendGreetingAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new JsonLogGreeting(_clientName),
                LogServerJsonContext.Default.JsonLogGreeting);
            var bytes = Utf8NoBom.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static async Task<bool> TryWritePipeAsync(Stream stream, LogEntry entry, CancellationToken ct)
    {
        try
        {
            var wire = new JsonLogEntry(
                Category: entry.Category,
                Level: entry.Level.ToString(),
                Message: entry.Message,
                Exception: entry.Exception?.ToString(),
                CorrelationId: entry.CorrelationId);
            var json = JsonSerializer.Serialize(wire, LogServerJsonContext.Default.JsonLogEntry);
            var bytes = Utf8NoBom.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static void WriteStderr(LogEntry entry)
    {
        try
        {
            var prefix = entry.CorrelationId is null ? string.Empty : $"[{entry.CorrelationId}] ";
            var line = $"{prefix}{entry.Level}: {entry.Category}: {entry.Message}";
            Console.Error.WriteLine(line);
            if (entry.Exception is not null)
            {
                Console.Error.WriteLine(entry.Exception);
            }
        }
        catch (IOException)
        {
            // stderr is gone — nothing we can do.
        }
    }
}
