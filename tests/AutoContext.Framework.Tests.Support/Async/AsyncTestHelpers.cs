namespace AutoContext.Framework.Tests.Support.Async;

/// <summary>
/// Polling helpers for async tests that need to wait until an
/// observable side effect (a captured buffer, a counter, a flag)
/// settles before asserting. Centralised so individual tests
/// don't re-roll the same deadline + delay loop with subtly
/// different defaults.
/// </summary>
public static class AsyncTestHelpers
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Polls <paramref name="predicate"/> on the calling thread until it
    /// returns <see langword="true"/>, throwing
    /// <see cref="TimeoutException"/> once the (wall-clock)
    /// <paramref name="timeout"/> elapses.
    /// </summary>
    public static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        var delay = pollInterval ?? DefaultPollInterval;

        while (!predicate())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("AsyncTestHelpers.WaitUntilAsync timed out.");
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
