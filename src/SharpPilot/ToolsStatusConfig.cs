namespace SharpPilot;

using System.Text.Json;

/// <summary>
/// Reads tool enable/disable state from a <c>tools-status.json</c> file
/// located next to the server binary. When the file is missing or unreadable,
/// all tools are treated as enabled.
/// </summary>
internal static class ToolsStatusConfig
{
    private static readonly string StatusFilePath =
        Path.Combine(AppContext.BaseDirectory, "tools-status.json");

    /// <summary>
    /// Returns <see langword="true"/> when the tool identified by
    /// <paramref name="toolName"/> is enabled (or when the status file
    /// is absent / does not contain the key).
    /// </summary>
    internal static bool IsEnabled(string toolName)
    {
        if (!File.Exists(StatusFilePath))
        {
            return true;
        }

        try
        {
            var json = File.ReadAllText(StatusFilePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty(toolName, out var value)
                && value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return true;
        }
    }
}
