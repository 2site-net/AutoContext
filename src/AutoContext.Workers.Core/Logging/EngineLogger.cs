namespace AutoContext.Workers.Core.Logging;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging;

/// <summary>
/// Per-category <see cref="ILogger"/> that materialises every
/// <see cref="ILogger.Log{TState}"/> call as a
/// <see cref="JsonLogRecord"/> and posts it to the
/// <see cref="EngineLogIngestRing"/> for delivery to the engine over
/// <c>Engine.WriteLog</c>. Used by <see cref="EngineLoggerProvider"/>;
/// one instance per (already worker-prefixed) category name.
/// </summary>
/// <remarks>
/// The record's <see cref="JsonLogRecord.Category"/> is the fully
/// composed <c>worker.&lt;workerId&gt;.&lt;category&gt;</c> string the
/// provider stamped, so the engine routes it to the emitting worker's
/// log. State-to-<see cref="JsonLogRecord.Properties"/> projection is
/// deliberately out of scope, mirroring the engine-side logger.
/// </remarks>
internal sealed class EngineLogger : ILogger
{
    private readonly string _category;
    private readonly EngineLogIngestRing _ring;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new per-category logger.
    /// </summary>
    /// <param name="category">The composed wire category (already
    /// carrying the <c>worker.&lt;workerId&gt;.</c> routing prefix).
    /// May be empty but never <see langword="null"/>.</param>
    /// <param name="ring">Buffer the materialised record is posted to.</param>
    /// <param name="timeProvider">Clock used to stamp the record.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.</exception>
    public EngineLogger(string category, EngineLogIngestRing ring, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _category = category;
        _ring = ring;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
        => logLevel != LogLevel.None;

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A formatter throwing must not bring down the worker's logging path; the failure mode is dropping the record, not propagating.")]
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
            return;
        }

        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var record = new JsonLogRecord
        {
            Timestamp = _timeProvider.GetUtcNow(),
            Category = _category,
            Level = MapLevel(logLevel),
            Message = message,
            EventId = ProjectEventId(eventId),
            Exception = ProjectException(exception),
        };

        _ring.Post(record);
    }

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

    private static JsonLogEventId? ProjectEventId(EventId eventId)
    {
        if (eventId.Id == 0 && string.IsNullOrEmpty(eventId.Name))
        {
            return null;
        }

        return new JsonLogEventId
        {
            Id = eventId.Id,
            Name = string.IsNullOrEmpty(eventId.Name) ? null : eventId.Name,
        };
    }

    private static JsonLogExceptionInfo? ProjectException(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        return new JsonLogExceptionInfo
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
