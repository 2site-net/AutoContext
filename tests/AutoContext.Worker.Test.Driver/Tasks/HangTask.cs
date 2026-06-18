namespace AutoContext.Worker.Test.Driver.Tasks;

using System.Text.Json;

/// <summary>
/// Deterministic cancellation task. Waits indefinitely, observing the
/// dispatched cancellation token, so the integration suite can cancel a
/// call mid-flight and assert the engine forwards cancellation cleanly
/// through the worker pipe.
/// </summary>
internal sealed class HangTask : ITestDriverTask
{
    /// <inheritdoc/>
    public string TaskName => "test_hang";

    /// <inheritdoc/>
    public async Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return data;
    }
}
