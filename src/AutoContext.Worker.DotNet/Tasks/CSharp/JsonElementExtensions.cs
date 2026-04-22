namespace AutoContext.Worker.DotNet.Tasks.CSharp;

using System.Text.Json;

/// <summary>
/// Shared <see cref="JsonElement"/> helpers used by the C# analyzer tasks
/// when reading optional string properties (file paths, EditorConfig keys,
/// namespaces) from a request envelope.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Returns the string value of <paramref name="property"/> on
    /// <paramref name="data"/>, or <c>null</c> if the property is missing
    /// or not a JSON string.
    /// </summary>
    public static string? TryGetString(this JsonElement data, string property)
    {
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return null;
    }
}
