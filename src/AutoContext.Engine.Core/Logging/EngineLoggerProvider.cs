namespace AutoContext.Engine.Core.Logging;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that materialises every engine
/// <see cref="ILogger{T}"/> record as a
/// <see cref="Protocol.Messages.Logs.LogRecord"/> on the shared
/// <see cref="LogChannel"/>. Hands out one <see cref="EngineLogger"/>
/// per category and caches them so the per-category instance is
/// stable for the lifetime of the host (matching the
/// <c>Microsoft.Extensions.Logging</c> convention every other
/// provider follows).
/// </summary>
/// <remarks>
/// <para>
/// The provider does not own the channel: <see cref="LogChannel"/>
/// is a singleton in the engine's DI graph, completed and drained
/// by <see cref="LogFileSinkService"/> on host shutdown. Disposing
/// this provider clears the per-category cache but never completes
/// or otherwise touches the channel — the file sink keeps the
/// final word on its row, and the host disposes services in
/// reverse registration order so the channel is still live when
/// this provider's <see cref="Dispose"/> runs.
/// </para>
/// <para>
/// The provider is registered alongside the framework default
/// <see cref="ILoggerProvider"/> set, so engine records continue
/// to reach the console and debug providers <c>Host.CreateApplicationBuilder</c>
/// installs by default. This row introduces engine → channel
/// routing; row 5 will introduce the broadcaster sibling and row
/// 8 introduces worker-bound routing via <c>Engine.WriteLog</c>.
/// </para>
/// </remarks>
internal sealed class EngineLoggerProvider : ILoggerProvider
{
    private readonly LogChannel _channel;
    private readonly ConcurrentDictionary<string, EngineLogger> _loggers = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new provider that routes every engine
    /// <see cref="ILogger"/> through <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel">Ingest channel shared with
    /// <see cref="LogFileSinkService"/>. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="timeProvider">Clock used to stamp
    /// <see cref="Protocol.Messages.Logs.LogRecord.Timestamp"/> on
    /// every emitted record. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public EngineLoggerProvider(LogChannel channel, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _channel = channel;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(
            categoryName ?? string.Empty,
            name => new EngineLogger(name, _channel, _timeProvider));

    /// <inheritdoc />
    /// <remarks>
    /// Clears the per-category cache. Does not dispose the
    /// <see cref="LogChannel"/> — see the type's remarks.
    /// </remarks>
    public void Dispose()
        => _loggers.Clear();
}
