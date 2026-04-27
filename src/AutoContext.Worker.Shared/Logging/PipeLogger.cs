namespace AutoContext.Worker.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Per-category <see cref="ILogger"/> that formats every entry on the
/// caller's thread (cheap) and hands the resulting <see cref="LogEntry"/>
/// to <see cref="LoggingClient"/> for off-thread delivery.
/// </summary>
internal sealed class PipeLogger(string category, LoggingClient client) : ILogger
{
    private readonly string _category = category;
    private readonly LoggingClient _client = client;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        _client.Post(new LogEntry(_category, logLevel, message, exception, CorrelationScope.Current));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
