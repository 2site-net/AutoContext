namespace AutoContext.Engine.Core.McpServer;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Builds the MCP <c>inputSchema</c> JSON-Schema object advertised over
/// <c>tools/list</c> from a tool's declared parameters. The schema is
/// data-driven — it mirrors the registry declaration order and marks the
/// required parameters — so the advertised contract stays decoupled from
/// any C# method signature (the MCP SDK's delegate-derived schema builder
/// is deliberately not used).
/// </summary>
internal static class InputSchemaBuilder
{
    /// <summary>
    /// Renders the supplied <paramref name="parameters"/> into a JSON-Schema
    /// <c>object</c> element with a <c>properties</c> map and, when any
    /// parameter is required, a <c>required</c> array.
    /// </summary>
    /// <param name="parameters">The tool parameters in declaration order.</param>
    /// <returns>The advertised <c>inputSchema</c> element.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="parameters"/> is <see langword="null"/>.
    /// </exception>
    public static JsonElement Build(IReadOnlyList<McpToolsRegistryParameterEntry> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in parameters)
        {
            properties[parameter.Name] = new JsonObject
            {
                ["type"] = parameter.Type,
                ["description"] = parameter.Description,
            };

            if (parameter.Required)
            {
                required.Add(parameter.Name);
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
