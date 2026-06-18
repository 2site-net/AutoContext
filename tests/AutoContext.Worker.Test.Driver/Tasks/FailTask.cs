namespace AutoContext.Worker.Test.Driver.Tasks;

using System.Text.Json;

/// <summary>
/// Deterministic failure task. Always throws so the dispatcher returns an
/// <c>error</c> status envelope, which the engine maps to the
/// <c>tool-error</c> arm of <c>McpTools.Invoke</c> — the integration
/// suite's hook for the worker-reported-failure path.
/// </summary>
internal sealed class FailTask : ITestDriverTask
{
    /// <summary>The fixed failure message tests assert against.</summary>
    internal const string FailureMessage = "test_fail task deliberately failed.";

    /// <inheritdoc/>
    public string TaskName => "test_fail";

    /// <inheritdoc/>
    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken)
        => throw new InvalidOperationException(FailureMessage);
}
