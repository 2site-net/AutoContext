namespace AutoContext.Mcp.Server.Tests.Support.Tools.Results;

using System.Text.Json;

using AutoContext.Mcp.Server.Tools.Results;
using AutoContext.Mcp.Server.Workers.Protocol;

/// <summary>
/// Shared fixture builders for the
/// <see cref="ToolResultComposer"/> tests — typed
/// <see cref="ToolResultComposerInput"/> + canonical OK / error
/// <see cref="JsonTaskResponse"/> shapes + a
/// <see cref="JsonElement"/> parser for embedded JSON literals.
/// </summary>
internal static class ToolResultComposerInputFactory
{
    public static ToolResultComposerInput Input(JsonTaskResponse response, int elapsedMs) =>
        new() { Response = response, ElapsedMs = elapsedMs };

    public static JsonTaskResponse OkResponse(string name, JsonElement output) => new()
    {
        McpTask = name,
        Status = JsonTaskResponse.StatusOk,
        Output = output,
        Error = string.Empty,
    };

    public static JsonTaskResponse ErrorResponse(string name, string error) => new()
    {
        McpTask = name,
        Status = JsonTaskResponse.StatusError,
        Output = null,
        Error = error,
    };

    public static JsonElement JsonElementFrom(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);
}
