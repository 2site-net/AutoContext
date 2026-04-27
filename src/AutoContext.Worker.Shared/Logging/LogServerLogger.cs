namespace AutoContext.Worker.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Per-category <see cref="ILogger"/> that formats every entry on the
/// caller's thread (cheap) and hands the resulting <see cref="LogRecord"/>
/// to <see cref="LogServerClient"/> for off-thread delivery.
/// </summary>
internal sealed class LogServerLogger(string category, LogServerClient client) : ILogger
{
    private readonly string _category = category;
    private readonly LogServerClient _client = client;

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

        _client.Enqueue(new LogRecord(_category, logLevel, message, exception, CorrelationScope.Current));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
