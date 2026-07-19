namespace AutoContext.Engine.Core.McpServer.Tools;

using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Reads values out of an MCP <c>tools/call</c> argument map by name and
/// kind, returning <see langword="null"/> when the key is absent or the JSON
/// kind does not match. Shared by the tool leaves so each one's argument →
/// RPC-param translation stays a few declarative lines.
/// </summary>
internal static class JsonArguments
{
    /// <summary>
    /// Serializes the argument map to a JSON object element, or
    /// <see langword="null"/> when there are no arguments.
    /// </summary>
    public static JsonElement? ToElement(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(arguments);
    }

    /// <summary>Reads a boolean argument, or <see langword="null"/>.</summary>
    public static bool? TryGetBool(IDictionary<string, JsonElement>? arguments, string name)
        => arguments is not null
            && arguments.TryGetValue(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;

    /// <summary>Reads a 32-bit integer argument, or <see langword="null"/>.</summary>
    public static int? TryGetInt(IDictionary<string, JsonElement>? arguments, string name)
        => arguments is not null
            && arguments.TryGetValue(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
                ? number
                : null;

    /// <summary>Reads a string argument, or <see langword="null"/>.</summary>
    public static string? TryGetString(IDictionary<string, JsonElement>? arguments, string name)
        => arguments is not null
            && arguments.TryGetValue(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    /// <summary>Reads an object argument, or <see langword="null"/>.</summary>
    public static JsonElement? TryGetObject(IDictionary<string, JsonElement>? arguments, string name)
        => arguments is not null
            && arguments.TryGetValue(name, out var value)
            && value.ValueKind == JsonValueKind.Object
                ? value
                : null;

    /// <summary>
    /// Reads a string-array argument (non-string elements skipped), or
    /// <see langword="null"/> when the key is absent or not an array.
    /// </summary>
    public static IReadOnlyList<string>? TryGetStringArray(
        IDictionary<string, JsonElement>? arguments, string name)
    {
        if (arguments is null
            || !arguments.TryGetValue(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<string>();

        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String && element.GetString() is { } text)
            {
                items.Add(text);
            }
        }

        return items;
    }
}
