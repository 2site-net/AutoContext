namespace AutoContext.Framework.Logging;

using System.Text;
using System.Text.Json;

using AutoContext.Framework.Transport;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Background pipe-client that drains <see cref="LogEntry"/> values
/// from a bounded channel and writes them as NDJSON over a named pipe
/// to the extension-side LogServer. When the pipe is unavailable (no
/// pipe name, connect attempt fails, or the pipe subsequently breaks)
/// the client transparently falls back to writing each record to
/// stderr in a human-readable single-line format.
/// </summary>
/// <remarks>
/// Composed over <see cref="PipeStreamingClient{T}"/>. This type owns
/// the wire shape (NDJSON entry + greeting) and the stderr fallback
/// format; the streaming primitive owns connect, queue, drain, and
/// disposal.
/// </remarks>
public sealed class LoggingClient : IAsyncDisposable
{
    private const int ConnectTimeoutMs = 2000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly PipeStreamingClient<LogEntry> _stream;

    /// <summary>
    /// Creates a new <see cref="LoggingClient"/>. Pass <see langword="null"/>
    /// or empty for <paramref name="pipeName"/> to force stderr fallback
    /// (useful for standalone runs and tests).
    /// </summary>
    public LoggingClient(string? pipeName, string clientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);
        _stream = new PipeStreamingClient<LogEntry>(
            transport,
            pipeName ?? string.Empty,
            serialize: SerializeEntry,
            logger: NullLogger<PipeStreamingClient<LogEntry>>.Instance,
            greeting: SerializeGreeting(clientName),
            fallback: WriteStderr,
            connectTimeoutMs: ConnectTimeoutMs);
    }

    /// <summary>
    /// Posts <paramref name="entry"/> for off-thread delivery. Never
    /// blocks; if the internal buffer is full the oldest entry is dropped.
    /// </summary>
    public void Post(LogEntry entry) => _stream.Post(entry);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _stream.DisposeAsync();

    private static byte[] SerializeGreeting(string clientName)
    {
        var json = JsonSerializer.Serialize(
            new JsonLogGreeting(clientName),
            LogServerJsonContext.Default.JsonLogGreeting);
        return Utf8NoBom.GetBytes(json + "\n");
    }

    private static ReadOnlyMemory<byte> SerializeEntry(LogEntry entry)
    {
        var wire = new JsonLogEntry(
            Category: entry.Category,
            Level: entry.Level.ToString(),
            Message: entry.Message,
            Exception: entry.Exception?.ToString(),
            CorrelationId: entry.CorrelationId);
        var json = JsonSerializer.Serialize(wire, LogServerJsonContext.Default.JsonLogEntry);
        return Utf8NoBom.GetBytes(json + "\n");
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
