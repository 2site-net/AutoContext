namespace AutoContext.Worker.Logging;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that hands out per-category loggers, all
/// of which enqueue records onto the shared <see cref="LogServerClient"/>.
/// Registered by <c>WorkerHostBuilderExtensions.ConfigureWorkerHost</c>
/// for every worker; the underlying <see cref="LogServerClient"/> decides
/// at runtime whether to ship records over the named pipe or fall back to
/// stderr.
/// </summary>
internal sealed class LogServerLoggerProvider : ILoggerProvider
{
    private readonly LogServerClient _client;
    private readonly ConcurrentDictionary<string, LogServerLogger> _loggers = new(StringComparer.Ordinal);

    public LogServerLoggerProvider(LogServerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new LogServerLogger(name, _client));

    // The LogServerClient is owned by the DI container (Singleton) and is
    // disposed by the host on shutdown — this provider must NOT dispose it.
    public void Dispose() => _loggers.Clear();
}
