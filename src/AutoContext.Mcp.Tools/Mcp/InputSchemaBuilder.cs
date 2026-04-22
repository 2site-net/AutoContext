namespace AutoContext.Mcp.Tools.Mcp;

using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Mcp.Tools.Manifest;

/// <summary>
/// Translates a manifest's <see cref="ManifestParameter"/> map into the
/// <c>{ "type": "object", "properties": {...}, "required": [...] }</c>
/// JSON Schema shape advertised to MCP clients as a tool's
/// <c>inputSchema</c>.
/// </summary>
/// <remarks>
/// JSON Schema treats <c>properties</c> as an unordered object and
/// <c>required</c> as a set, so this builder makes no guarantee about
/// the order of either — consumers index by name. The <c>required</c>
/// array is omitted when no parameter is required.
/// </remarks>
public static class InputSchemaBuilder
{
    /// <summary>
    /// Builds the input schema for the supplied <paramref name="parameters"/>.
    /// </summary>
    public static JsonElement Build(IReadOnlyDictionary<string, ManifestParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var (name, param) in parameters)
        {
            properties[name] = new JsonObject
            {
                ["type"] = param.Type,
                ["description"] = param.Description,
            };

            if (param.Required)
            {
                required.Add(name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return JsonSerializer.SerializeToElement(schema);
    }
}
