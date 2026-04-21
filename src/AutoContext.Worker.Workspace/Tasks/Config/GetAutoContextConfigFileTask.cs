namespace AutoContext.Worker.Workspace.Tasks.Config;

using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Mcp;
using AutoContext.Worker.Workspace.Hosting;

using Microsoft.Extensions.Options;

/// <summary>
/// <c>get_autocontext_config_file</c> — reads the workspace's <c>.autocontext.json</c>
/// and returns the canonical, expanded camelCase form.
/// </summary>
/// <remarks>
/// See architecture-centralized-mcp.md §4. Per-parent scoping for
/// <c>disabledTasks</c>; <c>false</c> shorthand expanded to
/// <c>{ "enabled": false, "disabledTasks": [] }</c>; both kebab-case
/// (<c>mcp-tools</c> / <c>disabled-features</c>) and camelCase keys are
/// accepted on input during the transition.
/// </remarks>
internal sealed class GetAutoContextConfigFileTask : IMcpTask
{
    private readonly string _workspaceRoot;

    public GetAutoContextConfigFileTask(IOptions<WorkerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var workspaceRoot = options.Value.WorkspaceRoot;

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new InvalidOperationException("Missing required configuration: --workspace-root");
        }

        _workspaceRoot = workspaceRoot;
    }

    public string TaskName => "get_autocontext_config_file";

    public async Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken ct)
    {
        var configPath = Path.Combine(_workspaceRoot, ".autocontext.json");
        var mcpTools = new JsonObject();

        if (File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var toolsElement = ReadToolsContainer(root);

            if (toolsElement is { ValueKind: JsonValueKind.Object } tools)
            {
                foreach (var tool in tools.EnumerateObject())
                {
                    mcpTools[tool.Name] = ExpandToolEntry(tool.Value);
                }
            }
        }

        var output = new JsonObject
        {
            ["mcpTools"] = mcpTools,
        };

        return JsonSerializer.SerializeToElement(output);
    }

    private static JsonElement? ReadToolsContainer(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("mcpTools", out var camel))
        {
            return camel;
        }

        if (root.TryGetProperty("mcp-tools", out var kebab))
        {
            return kebab;
        }

        return null;
    }

    private static JsonObject ExpandToolEntry(JsonElement entry)
    {
        // Shorthand: `false` => disabled with no per-task overrides.
        // Shorthand: `true`  => enabled with no per-task overrides.
        if (entry.ValueKind == JsonValueKind.False)
        {
            return new JsonObject
            {
                ["enabled"] = false,
                ["disabledTasks"] = new JsonArray(),
            };
        }

        if (entry.ValueKind == JsonValueKind.True)
        {
            return new JsonObject
            {
                ["enabled"] = true,
                ["disabledTasks"] = new JsonArray(),
            };
        }

        if (entry.ValueKind != JsonValueKind.Object)
        {
            // Unknown shape — surface a safe default rather than throwing.
            return new JsonObject
            {
                ["enabled"] = true,
                ["disabledTasks"] = new JsonArray(),
            };
        }

        var enabled = ReadBool(entry, "enabled", defaultValue: true);
        var disabled = ReadDisabledTasks(entry);
        var version = ReadOptionalString(entry, "version");

        var expanded = new JsonObject
        {
            ["enabled"] = enabled,
        };

        if (version is not null)
        {
            expanded["version"] = version;
        }

        expanded["disabledTasks"] = disabled;

        return expanded;
    }

    private static bool ReadBool(JsonElement entry, string key, bool defaultValue)
    {
        return !entry.TryGetProperty(key, out var value)
            ? defaultValue
            : value.ValueKind == JsonValueKind.True || (value.ValueKind != JsonValueKind.False && defaultValue);
    }

    private static string? ReadOptionalString(JsonElement entry, string key)
    {
        return entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static JsonArray ReadDisabledTasks(JsonElement entry)
    {
        var array = new JsonArray();

        // Prefer camelCase, fall back to legacy kebab.
        if (!entry.TryGetProperty("disabledTasks", out var list)
            && !entry.TryGetProperty("disabled-features", out list))
        {
            return array;
        }

        if (list.ValueKind != JsonValueKind.Array)
        {
            return array;
        }

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                array.Add(item.GetString());
            }
        }

        return array;
    }
}
