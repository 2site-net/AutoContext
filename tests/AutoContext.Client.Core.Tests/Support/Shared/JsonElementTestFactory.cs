namespace AutoContext.Client.Core.Tests.Support.Shared;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Builds the <see cref="JsonElement"/> payloads the client tests hand
/// to the connection and typed clients: one that serialises a wire DTO
/// through its source-generated type info, and one that parses a raw
/// JSON literal into an owned, detached element.
/// </summary>
internal static class JsonElementTestFactory
{
    /// <summary>
    /// Serialises <paramref name="value"/> to a <see cref="JsonElement"/>
    /// through its source-generated <paramref name="typeInfo"/>.
    /// </summary>
    public static JsonElement FromValue<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return JsonSerializer.SerializeToElement(value, typeInfo);
    }

    /// <summary>
    /// Parses <paramref name="json"/> into a detached
    /// <see cref="JsonElement"/> that outlives its backing document.
    /// </summary>
    public static JsonElement Parse(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
