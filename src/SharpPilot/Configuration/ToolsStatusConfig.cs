namespace SharpPilot.Configuration;

using System.Text.Json;

/// <summary>
/// Reads tool enable/disable state from a <c>.sharppilot.json</c> file
/// in the workspace root. When the file is missing or unreadable,
/// all tools are treated as enabled.
/// </summary>
internal static class ToolsStatusConfig
{
    private static string? s_configFilePath;

    /// <summary>
    /// Sets the workspace root so <see cref="IsEnabled"/> can locate
    /// <c>.sharppilot.json</c>. Call once at startup.
    /// </summary>
    internal static void Configure(string workspacePath) =>
        s_configFilePath = Path.Combine(workspacePath, ".sharppilot.json");

    /// <summary>
    /// Returns <see langword="true"/> when the tool identified by
    /// <paramref name="toolName"/> is enabled (or when the config file
    /// is absent / does not list the tool as disabled).
    /// </summary>
    internal static bool IsEnabled(string toolName)
    {
        var filePath = s_configFilePath;

        if (filePath is null || !File.Exists(filePath))
        {
            return true;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("tools", out var toolsElement)
                && toolsElement.TryGetProperty("disabledTools", out var disabledArray)
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
