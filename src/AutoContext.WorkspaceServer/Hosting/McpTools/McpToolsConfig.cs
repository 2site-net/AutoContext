namespace AutoContext.WorkspaceServer.Hosting.McpTools;

using System.Text.Json;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Reads tool enable/disable state from a <c>.autocontext.json</c> file
/// in the workspace root.  When the file is missing or unreadable,
/// all tools are treated as enabled.
/// </summary>
internal sealed class McpToolsConfig(IConfiguration configuration)
{
    /// <summary>
    /// Returns <see langword="true"/> when the tool identified by
    /// <paramref name="toolName"/> is enabled (or when the config file
    /// is absent / does not list the tool as disabled).
    /// </summary>
    internal bool IsEnabled(string toolName)
    {
        var workspaceRoot = configuration["workspace-root"];

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return true;
        }

        var configPath = Path.Combine(workspaceRoot, ".autocontext.json");

        if (!File.Exists(configPath))
        {
            return true;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("mcp-tools", out var toolsElement)
                || toolsElement.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            // Check if the tool itself is a top-level entry.
            if (toolsElement.TryGetProperty(toolName, out var entry))
            {
                // { "tool": false } — entirely disabled.
                if (entry.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                // { "tool": { "enabled": false } } — entirely disabled.
                if (entry.ValueKind == JsonValueKind.Object
                    && entry.TryGetProperty("enabled", out var enabledProp)
                    && enabledProp.ValueKind == JsonValueKind.False)
                {
                    return false;
                }
            }

            // Check if the tool appears in any entry's disabled-features array.
            foreach (var prop in toolsElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // If the parent is entirely disabled, its features are also disabled.
                if (prop.Value.TryGetProperty("enabled", out var parentEnabled)
                    && parentEnabled.ValueKind == JsonValueKind.False)
                {
                    if (prop.Value.TryGetProperty("disabled-features", out var allFeatures)
                        && allFeatures.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in allFeatures.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String
                                && string.Equals(item.GetString(), toolName, StringComparison.Ordinal))
                            {
                                return false;
                            }
                        }
                    }

                    continue;
                }

                if (!prop.Value.TryGetProperty("disabled-features", out var disabledFeatures)
                    || disabledFeatures.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in disabledFeatures.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String
                        && string.Equals(item.GetString(), toolName, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return true;
        }
    }
}
