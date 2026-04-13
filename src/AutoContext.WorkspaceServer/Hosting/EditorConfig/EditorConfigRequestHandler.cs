namespace AutoContext.WorkspaceServer.Hosting.EditorConfig;

using System.Text.Json;

/// <summary>
/// Handles pipe requests that resolve <c>.editorconfig</c> properties for a single file.
/// </summary>
internal sealed class EditorConfigRequestHandler(EditorConfigResolver resolver) : IRequestHandler
{
    public string RequestType => "editorconfig";

    public byte[] Process(ReadOnlySpan<byte> json)
    {
        var request = JsonSerializer.Deserialize<EditorConfigRequest>(json, WorkspaceService.JsonOptions);

        if (request is null || string.IsNullOrWhiteSpace(request.FilePath))
        {
            return JsonSerializer.SerializeToUtf8Bytes(new EditorConfigResponse([]), WorkspaceService.JsonOptions);
        }

        var properties = resolver.Resolve(request.FilePath, request.Keys);

        return JsonSerializer.SerializeToUtf8Bytes(new EditorConfigResponse(properties), WorkspaceService.JsonOptions);
    }
}
