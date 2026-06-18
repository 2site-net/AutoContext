namespace AutoContext.Worker.Test.Driver.Tasks;

using System.Text.Json;

/// <summary>
/// Deterministic happy-path task. Returns the received <c>data</c> payload
/// verbatim — including the <c>editorconfig.*</c> values the dispatcher
/// flattens in — so integration tests can assert the full
/// <c>McpTools.Invoke</c> → worker round-trip and the resulting <c>ok</c>
/// envelope content.
/// </summary>
internal sealed class EchoTask : ITestDriverTask
{
    /// <inheritdoc/>
    public string TaskName => "test_echo";

    /// <inheritdoc/>
    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken)
        => Task.FromResult(data);
}
