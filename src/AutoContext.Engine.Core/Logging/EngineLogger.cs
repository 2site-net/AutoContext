namespace AutoContext.Engine.Core.Logging;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging;

/// <summary>
/// Per-category <see cref="ILogger"/> that materialises every
/// <see cref="ILogger.Log{TState}"/> call as a
/// <see cref="LogRecord"/> on the shared <see cref="LogChannel"/>.
/// Used by <see cref="EngineLoggerProvider"/>; one instance per
/// category name.
/// </summary>
/// <remarks>
/// <para>
/// The logger formats the message on the caller's thread (cheap;
/// matches <see cref="Microsoft.Extensions.Logging"/>'s own provider
/// convention) and enqueues the resulting record via
/// <see cref="LogChannel.TryWrite"/>. The channel never blocks the
/// caller — when full, it evicts the oldest pending record per its
/// configured <c>FullMode = DropOldest</c> policy.
/// </para>
/// <para>
/// State → <see cref="LogRecord.Properties"/> projection is
/// deliberately deferred to a follow-up row. The wire envelope
/// already supports the field, but populating it for arbitrary
/// state objects without taking a reflective JSON serialisation
/// path (incompatible with the protocol assembly's
/// source-generation-only stance) needs a typed pipeline that is
/// out of scope here.
/// </para>
/// </remarks>
internal sealed class EngineLogger : ILogger
{
    private readonly string _category;
    private readonly LogChannel _channel;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new per-category logger.
    /// </summary>
    /// <param name="category">Category name as supplied by
    /// <see cref="ILoggerFactory.CreateLogger(string)"/>.
    /// May be <see cref="string.Empty"/> but never
    /// <see langword="null"/>.</param>
    /// <param name="channel">Ingest channel records are enqueued
    /// onto. Must not be <see langword="null"/>.</param>
    /// <param name="timeProvider">Clock used to stamp
    /// <see cref="LogRecord.Timestamp"/>. Must not be
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public EngineLogger(string category, LogChannel channel, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _category = category;
        _channel = channel;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Scopes are not surfaced on
    /// <see cref="LogRecord.Properties"/> in this row; returning
    /// the no-op disposable keeps callers that wrap log calls in
    /// <c>using (logger.BeginScope(...))</c> blocks working without
    /// allocating a per-call scope object.
    /// </remarks>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    /// <inheritdoc />
    /// <remarks>
    /// Returns <see langword="false"/> only for
    /// <see cref="LogLevel.None"/>; level-based filtering is the
    /// <see cref="ILoggerFactory"/>'s responsibility (configured
    /// via <c>builder.Logging</c>) so this provider does not need
    /// to consult any local options.
    /// </remarks>
    public bool IsEnabled(LogLevel logLevel)
        => logLevel != LogLevel.None;

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A formatter or exception toString throwing must not bring down the host's logging path; the failure mode is dropping the record, not propagating.")]
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

        string message;

        try
        {
            message = formatter(state, exception) ?? string.Empty;
        }
        catch
        {
            // A misbehaving formatter must not crash the logging
            // path. Drop the record silently — the producer will
            // see no record on the channel and the host carries on.
            return;
        }

        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var record = new LogRecord
        {
            Timestamp = _timeProvider.GetUtcNow(),
            Category = _category,
            Level = MapLevel(logLevel),
            Message = message,
            EventId = ProjectEventId(eventId),
            Exception = ProjectException(exception),
        };

        _channel.TryWrite(record);
    }

    /// <summary>
    /// Maps a <see cref="LogLevel"/> to the matching
    /// <see cref="LogLevels"/> wire constant. The caller has
    /// already short-circuited <see cref="LogLevel.None"/> via
    /// <see cref="IsEnabled"/>; mapping it here would never run.
    /// </summary>
    private static string MapLevel(LogLevel logLevel)
        => logLevel switch
        {
            LogLevel.Trace => LogLevels.Trace,
            LogLevel.Debug => LogLevels.Debug,
            LogLevel.Information => LogLevels.Information,
            LogLevel.Warning => LogLevels.Warning,
            LogLevel.Error => LogLevels.Error,
            LogLevel.Critical => LogLevels.Critical,
            LogLevel.None => LogLevels.Information,
            _ => LogLevels.Information,
        };

    /// <summary>
    /// Projects an <see cref="EventId"/> to the wire
    /// <see cref="LogEventId"/>. Returns <see langword="null"/>
    /// when the producer did not mint one (i.e. the default
    /// <c>default(EventId)</c> with id <c>0</c> and no name) so
    /// the field is omitted from the wire JSON.
    /// </summary>
    private static LogEventId? ProjectEventId(EventId eventId)
    {
        if (eventId.Id == 0 && string.IsNullOrEmpty(eventId.Name))
        {
            return null;
        }

        return new LogEventId
        {
            Id = eventId.Id,
            Name = string.IsNullOrEmpty(eventId.Name) ? null : eventId.Name,
        };
    }

    /// <summary>
    /// Projects an <see cref="Exception"/> to the wire
    /// <see cref="LogExceptionInfo"/>, walking the
    /// <see cref="Exception.InnerException"/> chain depth-first.
    /// Returns <see langword="null"/> when <paramref name="exception"/>
    /// is <see langword="null"/>.
    /// </summary>
    private static LogExceptionInfo? ProjectException(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        return new LogExceptionInfo
        {
            Type = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message ?? string.Empty,
            StackTrace = exception.StackTrace ?? string.Empty,
            Inner = ProjectException(exception.InnerException),
        };
    }

    private sealed class NullScope : IDisposable
    {
        /// <summary>Shared singleton instance.</summary>
        public static readonly NullScope Instance = new();

        /// <summary>No-op disposal; the scope owns no resources.</summary>
        public void Dispose()
        {
            // Intentionally empty: BeginScope returns a singleton
            // no-op disposable so callers' using-blocks compile and
            // run without allocating per-call scope objects.
        }
    }
}
