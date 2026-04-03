namespace SharpPilot.WorkspaceServer.Features.McpTools;

using System.Text.Json;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Reads tool enable/disable state from a <c>.sharppilot.json</c> file
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

        var configPath = Path.Combine(workspaceRoot, ".sharppilot.json");

        if (!File.Exists(configPath))
        {
            return true;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("mcp-tools", out var toolsElement)
                && toolsElement.TryGetProperty("disabled", out var disabledArray)
                && disabledArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in disabledArray.EnumerateArray())
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
