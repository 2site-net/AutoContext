namespace AutoContext.Engine.Core.Features.McpTools;

using System.Collections.Concurrent;
using System.Text.Json;

using Json.Schema;

/// <summary>
/// Validates the raw <c>mcp-tools-catalog.json</c> text against its bundled
/// <c>mcp-tools-catalog.schema.json</c>. Pure: it takes the two JSON strings
/// and returns a <see cref="McpToolsRegistryValidationResult"/>, leaving the
/// throw-on-defect decision to <see cref="McpToolsRegistryLoader"/>.
/// </summary>
internal static class McpToolsCatalogSchemaValidator
{
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> SchemaCache =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Validates <paramref name="catalogJson"/> against
    /// <paramref name="schemaJson"/>.
    /// </summary>
    /// <param name="catalogJson">The raw catalog JSON text.</param>
    /// <param name="schemaJson">The raw schema JSON text.</param>
    /// <returns>The validation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalogJson"/>
    /// or <paramref name="schemaJson"/> is <see langword="null"/>.</exception>
    public static McpToolsRegistryValidationResult Validate(
        string catalogJson,
        string schemaJson)
    {
        ArgumentNullException.ThrowIfNull(catalogJson);
        ArgumentNullException.ThrowIfNull(schemaJson);

        var errors = new List<string>();

        JsonDocument catalogDocument;

        try
        {
            catalogDocument = JsonDocument.Parse(catalogJson);
        }
        catch (JsonException exception)
        {
            errors.Add($"Catalog is not valid JSON: {exception.Message}");
            return new McpToolsRegistryValidationResult(errors);
        }

        using (catalogDocument)
        {
            ValidateAgainstSchema(catalogDocument, schemaJson, errors);
        }

        return new McpToolsRegistryValidationResult(errors);
    }

    private static void ValidateAgainstSchema(
        JsonDocument catalogDocument,
        string schemaJson,
        List<string> errors)
    {
        var schema = SchemaCache
            .GetOrAdd(schemaJson, static text => new Lazy<JsonSchema>(() => JsonSchema.FromText(text)))
            .Value;
        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        var evaluation = schema.Evaluate(catalogDocument.RootElement, options);

        if (evaluation.IsValid)
        {
            return;
        }

        var reportedAnyError = false;

        foreach (var detail in evaluation.Details ?? [])
        {
            if (detail.IsValid || detail.Errors is null || detail.Errors.Count == 0)
            {
                continue;
            }

            var instanceLocation = detail.InstanceLocation.ToString();
            var instancePath = string.IsNullOrEmpty(instanceLocation) ? "(root)" : instanceLocation;

            foreach (var (keyword, message) in detail.Errors)
            {
                errors.Add($"Schema error at {instancePath}: {keyword} - {message}");
                reportedAnyError = true;
            }
        }

        if (!reportedAnyError)
        {
            errors.Add("Catalog failed JSON Schema validation (no further detail).");
        }
    }
}
