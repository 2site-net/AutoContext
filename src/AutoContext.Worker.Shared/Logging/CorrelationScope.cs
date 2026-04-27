namespace AutoContext.Worker.Logging;

/// <summary>
/// Worker-process holder for the per-invocation correlation id sent on
/// every <c>TaskRequest</c>. Set by <c>McpToolService</c> for the
/// duration of a task dispatch and read by <c>LogServerLogger</c> at
/// log-emission time so every <c>LogRecord</c> carries the id without
/// requiring callers to thread it through their <c>ILogger</c> APIs.
/// </summary>
/// <remarks>
/// Implemented over <see cref="AsyncLocal{T}"/> so the scope is bound
/// to the async call chain that originated from the dispatch — nested
/// <c>Task</c>s and <c>await</c>s observe the same id, but unrelated
/// dispatches running concurrently each see their own.
/// </remarks>
internal static class CorrelationScope
{
    private static readonly AsyncLocal<string?> Holder = new();

    /// <summary>
    /// The correlation id active on the current async context, or
    /// <c>null</c> when no <see cref="Push"/> scope is in effect.
    /// </summary>
    public static string? Current => Holder.Value;

    /// <summary>
    /// Sets <see cref="Current"/> to <paramref name="correlationId"/>
    /// for the lifetime of the returned <see cref="IDisposable"/>;
    /// disposing restores whatever id was previously in effect.
    /// </summary>
    public static IDisposable Push(string correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);

        var previous = Holder.Value;
        Holder.Value = correlationId;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Holder.Value = previous;
        }
    }
}
