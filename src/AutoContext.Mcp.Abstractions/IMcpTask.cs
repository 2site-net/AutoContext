namespace AutoContext.Mcp;

using System.Text.Json;

/// <summary>
/// A single MCP Task as executed by a worker process.
/// </summary>
/// <remarks>
/// MCP Tasks are the worker-facing execution units. Each one is dispatched
/// over a named pipe by <c>McpToolClient</c> (in <c>AutoContext.Mcp.Server</c>)
/// when an MCP Tool that references it is invoked.
/// <para>
/// The contract is JSON-native end to end: <paramref name="data"/> is the
/// payload Copilot supplied to the parent MCP Tool, and the return value
/// is whatever JSON the task wants to surface (per-tool <c>outputSchema</c>
/// in <c>mcp-tools-manifest.json</c> documents the shape).
/// </para>
/// </remarks>
public interface IMcpTask
{
    /// <summary>
    /// Snake_case identifier matching the task's <c>name</c> in
    /// <c>mcp-tools-manifest.json</c>.
    /// </summary>
    string TaskName { get; }

    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <param name="data">
    /// The JSON payload from the parent MCP Tool invocation. EditorConfig
    /// values declared by the task in <c>mcp-tools-manifest.json</c> are merged in
    /// as flat properties prefixed with <c>editorconfig.</c> (e.g.
    /// <c>data["editorconfig.indent_style"]</c>); missing keys are simply
    /// absent.
    /// </param>
    /// <param name="ct">Cancellation token threaded from the MCP SDK through the pipe protocol.</param>
    Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken ct);
}
