namespace AutoContext.Workers.Core.Logging;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// Worker-side <see cref="ILoggerProvider"/> that routes every
/// <see cref="ILogger{T}"/> record to the engine over
/// <c>Engine.WriteLog</c>. Each per-category logger stamps its
/// records with the <c>worker.&lt;workerId&gt;.</c> routing prefix so
/// the engine lands them in the emitting worker's log, then posts them
/// to the shared <see cref="EngineLogIngestRing"/>.
/// </summary>
/// <remarks>
/// Hands out one <see cref="EngineLogger"/> per category and caches
/// it for the lifetime of the host, matching the
/// <c>Microsoft.Extensions.Logging</c> convention. The provider does
/// not own the ring — the composition root registers and disposes it
/// — so <see cref="Dispose"/> only clears the per-category cache.
/// </remarks>
public sealed class EngineLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, EngineLogger> _loggers = new(StringComparer.Ordinal);
    private readonly EngineLogIngestRing _ring;
    private readonly TimeProvider _timeProvider;
    private readonly string _workerId;

    /// <summary>
    /// Creates a new provider that routes every worker
    /// <see cref="ILogger"/> through <paramref name="ring"/>.
    /// </summary>
    /// <param name="workerId">The worker's stable short identifier
    /// (for example <c>dotnet</c>) stamped onto every record's
    /// routing category.</param>
    /// <param name="ring">Buffer materialised records are posted to.</param>
    /// <param name="timeProvider">Clock used to stamp every emitted
    /// record's timestamp.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="workerId"/>, <paramref name="ring"/>, or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workerId"/> is empty or whitespace.</exception>
    public EngineLoggerProvider(string workerId, EngineLogIngestRing ring, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _workerId = workerId;
        _ring = ring;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(
            categoryName ?? string.Empty,
            name => new EngineLogger(
                WorkerLogCategory.Compose(_workerId, name), _ring, _timeProvider));

    /// <inheritdoc />
    public void Dispose()
        => _loggers.Clear();
}
