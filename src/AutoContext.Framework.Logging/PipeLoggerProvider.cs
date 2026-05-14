namespace AutoContext.Framework.Logging;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that hands out per-category loggers, all
/// of which enqueue records onto the shared <see cref="LoggingClient"/>.
/// The underlying <see cref="LoggingClient"/> decides at runtime whether
/// to ship records over the named pipe or fall back to stderr.
/// </summary>
public sealed class PipeLoggerProvider : ILoggerProvider
{
    private readonly LoggingClient _client;
    private readonly ConcurrentDictionary<string, PipeLogger> _loggers = new(StringComparer.Ordinal);

    public PipeLoggerProvider(LoggingClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new PipeLogger(name, _client));

    // The LoggingClient is owned by the DI container (Singleton) and is
    // disposed by the host on shutdown — this provider must NOT dispose it.
    public void Dispose() => _loggers.Clear();
}
