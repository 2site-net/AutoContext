namespace AutoContext.WorkspaceServer.Features.McpTools;

using System.Text.Json;

using AutoContext.WorkspaceServer.Features.EditorConfig;
using AutoContext.WorkspaceServer.Features.McpTools.Protocol;
using AutoContext.WorkspaceServer.Services;

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

        if (request is null
            || string.IsNullOrWhiteSpace(request.FilePath)
            || request.McpTools is not { Length: > 0 })
        {
            return JsonSerializer.SerializeToUtf8Bytes(new McpToolsResponse([]), WorkspaceService.JsonOptions);
        }

        var results = new McpToolEditorConfigResult[request.McpTools.Length];

        for (var i = 0; i < request.McpTools.Length; i++)
        {
            var tool = request.McpTools[i];
            var enabled = toolsStatus.IsEnabled(tool.Name);
            var hasKeys = tool.EditorConfigKeys is { Length: > 0 };

            if (enabled)
            {
                var data = hasKeys ? resolver.Resolve(request.FilePath, tool.EditorConfigKeys) : null;
                results[i] = new McpToolEditorConfigResult(tool.Name, McpToolMode.Run, data);
            }
            else if (hasKeys)
            {
                var data = resolver.Resolve(request.FilePath, tool.EditorConfigKeys);
                results[i] = new McpToolEditorConfigResult(tool.Name, McpToolMode.EditorConfigOnly, data);
            }
            else
            {
                results[i] = new McpToolEditorConfigResult(tool.Name, McpToolMode.Skip);
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(new McpToolsResponse(results), WorkspaceService.JsonOptions);
    }
}
