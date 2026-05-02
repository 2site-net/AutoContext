namespace AutoContext.Worker.Workspace.Tasks.EditorConfig;

using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Mcp;

/// <summary>
/// <c>get_editorconfig_rules</c> — resolves a filtered subset of effective
/// <c>.editorconfig</c> properties for a given file path.
/// </summary>
/// <remarks>
/// Request <c>data</c>:  <c>{ "path": "&lt;abs-path&gt;", "keys": ["k1", "k2", ...] }</c><br/>
/// Response <c>output</c>: flat <c>{ "k1": "v1", ... }</c> map; missing keys are omitted.
/// </remarks>
internal sealed class GetEditorConfigRulesTask : IMcpTask
{
    public string TaskName => "get_editorconfig_rules";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken)
    {
        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("path", out var pathElement)
            || pathElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("'data.path' is required and must be a string.");
        }

        var path = pathElement.GetString()!;
        var requestedKeys = ReadKeys(data);

        var resolved = EditorConfigResolver.Resolve(path);
        var filtered = new JsonObject();

        if (requestedKeys is null)
        {
            // No filter: return all resolved keys.
            foreach (var (key, value) in resolved)
            {
                filtered[key] = value;
            }
        }
        else
        {
            foreach (var key in requestedKeys)
            {
                if (resolved.TryGetValue(key, out var value))
                {
                    filtered[key] = value;
                }
            }
        }

        return Task.FromResult(JsonSerializer.SerializeToElement(filtered));
    }

    private static List<string>? ReadKeys(JsonElement data)
    {
        if (!data.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>(keys.GetArrayLength());

        foreach (var k in keys.EnumerateArray())
        {
            if (k.ValueKind == JsonValueKind.String)
            {
                list.Add(k.GetString()!);
            }
        }

        return list;
    }
}
