namespace AutoContext.Engine.Core.Features.McpTools;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;

using Json.Schema;

/// <summary>
/// Validates the raw <c>mcp-tools-registry.json</c> text against its
/// bundled <c>mcp-tools-registry.schema.json</c> and a small set of
/// cross-reference rules the schema cannot express. Pure: it takes the two
/// JSON strings and returns a <see cref="McpToolsRegistryValidationResult"/>,
/// leaving the throw-on-defect decision to
/// <see cref="McpToolsRegistryLoader"/>.
/// </summary>
/// <remarks>
/// The schema covers structural shape (required keys, name patterns,
/// non-empty descriptions, the parameter-spec shape, EditorConfig-key
/// uniqueness, <c>additionalProperties: false</c>). Two failure modes
/// remain that JSON Schema cannot catch, both of which would otherwise
/// yield a silently broken registry:
/// <list type="bullet">
///   <item><description><b>Duplicate tool name</b>: two <c>tools</c>
///   entries share a <c>name</c>. <c>uniqueItems</c> compares whole
///   objects, so it does not enforce uniqueness by the <c>name</c>
///   key.</description></item>
///   <item><description><b>Duplicate parameter name</b>: a tool's
///   <c>parameters</c> object declares the same key twice. Deserialising
///   into a dictionary would silently keep the last and drop the rest, so
///   the check reads the property names off the raw
///   <see cref="JsonDocument"/> (which preserves duplicates)
///   instead.</description></item>
/// </list>
/// </remarks>
internal static class McpToolsRegistrySchemaValidator
{
    /// <summary>
    /// Parsed schemas keyed by their raw text. <see cref="JsonSchema.FromText"/>
    /// registers a schema's <c>$id</c> into <c>Json.Schema</c>'s global
    /// registry as a side effect, and re-registering under the same <c>$id</c>
    /// throws. Caching the parsed instance per schema text means the <c>$id</c>
    /// registers exactly once and every later <see cref="Validate"/> call
    /// re-uses it. The value is a <see cref="Lazy{T}"/> because
    /// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>
    /// may invoke its factory on several threads at once under contention; the
    /// cheap <see cref="Lazy{T}"/> wrapper can be built more than once, but the
    /// side-effecting <see cref="JsonSchema.FromText"/> runs once — on the
    /// single instance the dictionary keeps.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> SchemaCache =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Validates <paramref name="registryJson"/> against
    /// <paramref name="schemaJson"/> and the cross-reference rules.
    /// </summary>
    /// <param name="registryJson">The raw registry JSON text.</param>
    /// <param name="schemaJson">The raw schema JSON text.</param>
    /// <returns>The validation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registryJson"/>
    /// or <paramref name="schemaJson"/> is <see langword="null"/>.</exception>
    public static McpToolsRegistryValidationResult Validate(
        string registryJson,
        string schemaJson)
    {
        ArgumentNullException.ThrowIfNull(registryJson);
        ArgumentNullException.ThrowIfNull(schemaJson);

        var errors = new List<string>();

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(registryJson);
        }
        catch (JsonException exception)
        {
            errors.Add($"Registry is not valid JSON: {exception.Message}");
            return new McpToolsRegistryValidationResult(errors);
        }

        using (document)
        {
            ValidateAgainstSchema(document, schemaJson, errors);
            ValidateNoDuplicateToolNames(document, errors);
            ValidateNoDuplicateParameterNames(document, errors);
        }

        return new McpToolsRegistryValidationResult(errors);
    }

    private static void ValidateAgainstSchema(
        JsonDocument document,
        string schemaJson,
        List<string> errors)
    {
        var schema = SchemaCache
            .GetOrAdd(schemaJson, static text => new Lazy<JsonSchema>(() => JsonSchema.FromText(text)))
            .Value;
        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        var result = schema.Evaluate(document.RootElement, options);

        if (result.IsValid)
        {
            return;
        }

        var any = false;

        foreach (var detail in result.Details ?? [])
        {
            if (detail.IsValid || detail.Errors is null || detail.Errors.Count == 0)
            {
                continue;
            }

            var location = detail.InstanceLocation.ToString();
            var path = string.IsNullOrEmpty(location) ? "(root)" : location;

            foreach (var (keyword, message) in detail.Errors)
            {
                errors.Add($"Schema error at {path}: {keyword} — {message}");
                any = true;
            }
        }

        if (!any)
        {
            errors.Add("Registry failed JSON Schema validation (no further detail).");
        }
    }

    private static void ValidateNoDuplicateToolNames(JsonDocument document, List<string> errors)
    {
        if (!TryGetArray(document.RootElement, "tools", out var tools))
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var tool in tools.EnumerateArray())
        {
            if (tool.ValueKind == JsonValueKind.Object
                && tool.TryGetProperty("name", out var name)
                && name.ValueKind == JsonValueKind.String
                && name.GetString() is { } toolName
                && !seen.Add(toolName))
            {
                errors.Add($"Duplicate tool name '{toolName}' at tools[{index}].");
            }

            index++;
        }
    }

    private static void ValidateNoDuplicateParameterNames(JsonDocument document, List<string> errors)
    {
        if (!TryGetArray(document.RootElement, "tools", out var tools))
        {
            return;
        }

        var index = 0;

        foreach (var tool in tools.EnumerateArray())
        {
            if (tool.ValueKind == JsonValueKind.Object
                && tool.TryGetProperty("parameters", out var parameters)
                && parameters.ValueKind == JsonValueKind.Object)
            {
                var toolLabel = tool.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String
                        ? $"'{name.GetString()}'"
                        : $"tools[{index}]";

                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var property in parameters.EnumerateObject())
                {
                    if (!seen.Add(property.Name))
                    {
                        errors.Add(
                            $"Duplicate parameter name '{property.Name}' in tool {toolLabel}.");
                    }
                }
            }

            index++;
        }
    }

    private static bool TryGetArray(JsonElement root, string propertyName, out JsonElement array)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }
}
