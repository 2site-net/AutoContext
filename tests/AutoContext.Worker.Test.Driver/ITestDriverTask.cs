namespace AutoContext.Worker.Test.Driver;

using System.Text.Json;

/// <summary>
/// A single behaviour the test driver serves over its worker pipe. This is
/// the driver's own minimal task contract — deliberately independent of
/// <c>AutoContext.Framework.Workers</c> so the driver is decoupled from the
/// worker-host framework and exercises only the engine's wire contract.
/// </summary>
internal interface ITestDriverTask
{
    /// <summary>
    /// Snake_case task name the engine's MCP-tools registry dispatches to
    /// (for example <c>test_echo</c>).
    /// </summary>
    string TaskName { get; }

    /// <summary>
    /// Runs the task against the request <paramref name="data"/> (with any
    /// <c>editorconfig.*</c> values already flattened in) and returns the
    /// JSON the engine surfaces as the tool result.
    /// </summary>
    Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken);
}
