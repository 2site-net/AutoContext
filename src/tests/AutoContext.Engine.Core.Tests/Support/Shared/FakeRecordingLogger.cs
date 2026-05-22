namespace AutoContext.Engine.Core.Tests.Support.Shared;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// Minimal <see cref="ILogger"/> fake that captures every emitted
/// entry so policy tests can assert log severity and event ids
/// without spinning up a real logging pipeline.
/// </summary>
internal sealed class FakeRecordingLogger : ILogger
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _entries.Enqueue(new LogEntry(
            logLevel,
            eventId,
            formatter(state, exception),
            exception));
    }

    public sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception);
}
