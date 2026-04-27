namespace AutoContext.Worker.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// In-process record enqueued by every worker <see cref="ILogger"/> call.
/// Drained off-thread by <see cref="LogServerClient"/> and either sent over
/// the pipe (extension is listening) or written to stderr (fallback).
/// </summary>
/// <remarks>
/// <c>EventId</c> is intentionally omitted from the v1 wire format — the
/// extension-side LogServer presents records as plain text in a
/// <c>LogOutputChannel</c>, where event ids carry no UI affordance.
/// Extending <see cref="LogRecord"/> and <see cref="LogRecordWire"/> to
/// carry an event id (and matching the change in the LogServer reader)
/// is the obvious next step if structured filtering arrives later.
/// </remarks>
internal readonly record struct LogRecord(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    string? CorrelationId);
