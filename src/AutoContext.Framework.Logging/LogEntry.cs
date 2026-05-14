namespace AutoContext.Framework.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// In-process record enqueued by every <see cref="ILogger"/> call routed
/// through <see cref="PipeLogger"/>. Drained off-thread by
/// <see cref="LoggingClient"/> and either sent over the pipe (extension
/// is listening) or written to stderr (fallback).
/// </summary>
/// <remarks>
/// <c>EventId</c> is intentionally omitted from the v1 wire format — the
/// extension-side LogServer presents records as plain text in a
/// <c>LogOutputChannel</c>, where event ids carry no UI affordance.
/// Extending <see cref="LogEntry"/> and <c>JsonLogEntry</c> to carry an
/// event id (and matching the change in the LogServer reader) is the
/// obvious next step if structured filtering arrives later.
/// </remarks>
public readonly record struct LogEntry(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    string? CorrelationId);
