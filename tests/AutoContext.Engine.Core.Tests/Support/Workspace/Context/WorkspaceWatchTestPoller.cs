namespace AutoContext.Engine.Core.Tests.Support.Workspace.Context;

using AutoContext.Engine.Core.Workspace.Context;

internal static class WorkspaceWatchTestPoller
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Polls <see cref="WorkspaceContextDetector.Current"/> until
    /// <paramref name="predicate"/> holds or a fixed deadline elapses,
    /// giving the live <see cref="FileSystemWatcher"/> time to
    /// deliver an event and the debouncer time to reclassify. Returns the
    /// final snapshot either way so the caller's assertion reports the
    /// observed flag set on timeout.
    /// </summary>
    internal static async Task<WorkspaceDetectionResult> WaitUntilAsync(
        WorkspaceContextDetector detector,
        Func<WorkspaceDetectionResult, bool> predicate,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + Timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var current = detector.Current;

            if (predicate(current))
            {
                return current;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return detector.Current;
    }
}
