namespace AutoContext.WorkspaceServer.Hosting.McpTools;

using System.Text.Json;

using AutoContext.WorkspaceServer.Hosting.EditorConfig;
using AutoContext.Mcp.Shared.McpTools;

/// <summary>
/// Handles pipe requests that resolve MCP tool modes and associated
/// <c>.editorconfig</c> data for a batch of tools.
/// </summary>
internal sealed class McpToolsRequestHandler(
    EditorConfigResolver resolver,
    McpToolsConfig toolsStatus) : IRequestHandler
{
    public string RequestType => "mcp-tools";

    public byte[] Process(ReadOnlySpan<byte> json)
    {
        var request = JsonSerializer.Deserialize<McpToolsRequest>(json, WorkspaceService.JsonOptions);

        if (request is null || request.Tools is not { Length: > 0 })
        {
            return JsonSerializer.SerializeToUtf8Bytes(new McpToolsResponse([]), WorkspaceService.JsonOptions);
        }

        // Concern 1: tool modes (from .autocontext.json)
        var tools = new Dictionary<string, bool>(request.Tools.Length);

        foreach (var tool in request.Tools)
        {
            tools[tool] = toolsStatus.IsEnabled(tool);
        }

        // Concern 2: EditorConfig (from .editorconfig files) — independent
        Dictionary<string, string>? editorConfig = null;

        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            var allProperties = resolver.Resolve(request.FilePath!);

            if (allProperties.Count > 0 && request.EditorConfigKeys is { Length: > 0 })
            {
                var keySet = new HashSet<string>(request.EditorConfigKeys, StringComparer.OrdinalIgnoreCase);
                editorConfig = allProperties
                    .Where(kv => keySet.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            else if (allProperties.Count > 0)
            {
                editorConfig = allProperties;
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(new McpToolsResponse(tools, editorConfig), WorkspaceService.JsonOptions);
    }
}
