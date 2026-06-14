namespace AutoContext.McpTools.Manifest.Generator;

using System.Text.Json;

/// <summary>
/// Projects the hand-authored <c>mcp-tools-registry.json</c> into the wire-shape
/// <c>mcp-tools.json</c> catalog. The projector flattens the registry's worker
/// groups into one tool list — worker grouping is dispatch metadata the catalog
/// does not carry — and copies each tool's name, description, and task names
/// verbatim. It drops the registry's input <c>parameters</c> (the
/// <c>McpTools.List</c> wire shape omits schemas) and per-task <c>editorconfig</c>
/// bindings (dispatch metadata). A missing or unparsable registry, a tool or task
/// without a name, a tool without a description, or two tools declaring the same
/// name all fail the build.
/// </summary>
internal sealed class McpToolsRegistryProjector : IMcpToolsRegistryProjector
{
    /// <inheritdoc />
    public JsonMcpToolsCatalog Project(string registryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(registryPath);

        var path = Path.GetFullPath(registryPath);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"[mcp-tools.json] registry file not found: {path}");
        }

        var registry = ReadRegistry(path);

        var tools = new List<JsonMcpToolEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var worker in registry.Workers ?? [])
        {
            foreach (var tool in worker.Tools ?? [])
            {
                var entry = ProjectTool(tool);

                if (!seen.Add(entry.Name))
                {
                    throw new InvalidOperationException($"[mcp-tools.json] duplicate tool name '{entry.Name}'.");
                }

                tools.Add(entry);
            }
        }

        return new JsonMcpToolsCatalog(registry.SchemaVersion, tools);
    }

    private static JsonMcpToolEntry ProjectTool(JsonRegistryTool tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            throw new InvalidOperationException("[mcp-tools.json] a tool is missing 'name'.");
        }

        if (string.IsNullOrWhiteSpace(tool.Description))
        {
            throw new InvalidOperationException($"[mcp-tools.json] tool '{tool.Name}' is missing 'description'.");
        }

        var tasks = new List<JsonMcpTaskEntry>();

        foreach (var task in tool.Tasks ?? [])
        {
            if (string.IsNullOrWhiteSpace(task.Name))
            {
                throw new InvalidOperationException($"[mcp-tools.json] tool '{tool.Name}' has a task missing 'name'.");
            }

            tasks.Add(new JsonMcpTaskEntry(task.Name));
        }

        return new JsonMcpToolEntry(tool.Name, tool.Description, tasks);
    }

    private static JsonRegistryDocument ReadRegistry(string path)
    {
        var fileName = Path.GetFileName(path);

        JsonRegistryDocument? registry;

        try
        {
            registry = JsonSerializer.Deserialize(
                File.ReadAllText(path),
                McpToolsManifestJsonContext.Default.JsonRegistryDocument);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"[mcp-tools.json] registry '{fileName}' is unparsable: {exception.Message}");
        }

        return registry
            ?? throw new InvalidOperationException($"[mcp-tools.json] registry '{fileName}' is empty.");
    }
}
