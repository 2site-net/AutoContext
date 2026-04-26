namespace AutoContext.Mcp.Server.Registry;

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

using Json.Schema;

/// <summary>
/// Validates a parsed <see cref="McpWorkersCatalog"/> (and its raw JSON form) against
/// the embedded <c>mcp-workers-registry.schema.json</c> and a set of fail-fast
/// cross-reference rules.
/// </summary>
/// <remarks>
/// Detected failure modes:
/// <list type="bullet">
///   <item><description><b>Schema</b>: registry doesn't conform to the JSON Schema.</description></item>
///   <item><description><b>Duplicate tool name</b>: two MCP Tools share a <c>name</c> anywhere in the registry.</description></item>
///   <item><description><b>Duplicate task name</b>: two MCP Tasks share a <c>name</c> within the same tool.</description></item>
///   <item><description><b>Duplicate worker name</b>: two worker entries share a <c>name</c> or an <c>endpoint</c>.</description></item>
///   <item><description><b>Empty collections</b>: registry has no workers, a worker has no tools, or a tool has no tasks.</description></item>
///   <item><description><b>Schema version shape</b>: <c>schemaVersion</c> isn't parseable as a non-negative integer.</description></item>
/// </list>
/// <para>
/// The emptiness and schema-version-shape checks duplicate constraints in the JSON Schema; they
/// are kept as belt-and-braces guards so a regression in the schema (or a future loosening of it)
/// cannot silently produce a registry that registers zero MCP tools — or a registry whose
/// <c>schemaVersion</c> is some arbitrary string. We deliberately don't double-check
/// <c>schemaVersion</c> against a specific expected value: the schema's <c>const: "1"</c>
/// already enforces equality, and any duplicate runtime check would have to compare against
/// the same constant.
/// </para>
/// </remarks>
public static class RegistrySchemeValidator
{
    private const string EmbeddedSchemaResourceName = "AutoContext.Mcp.Server.mcp-workers-registry.schema.json";

    private static readonly Lazy<JsonSchema> Schema = new(LoadEmbeddedSchema, isThreadSafe: true);

    /// <summary>
    /// Validates the supplied registry JSON against the schema and the cross-reference rules.
    /// </summary>
    /// <param name="registryJson">Raw JSON text for the registry.</param>
    /// <param name="registry">Parsed registry.</param>
    public static RegistrySchemeValidatorResult Validate(string registryJson, McpWorkersCatalog registry)
    {
        ArgumentNullException.ThrowIfNull(registryJson);
        ArgumentNullException.ThrowIfNull(registry);

        var errors = new List<string>();

        ValidateAgainstSchema(registryJson, errors);
        ValidateCrossReferences(registry, errors);

        return new RegistrySchemeValidatorResult(errors);
    }

    private static void ValidateAgainstSchema(string registryJson, List<string> errors)
    {
        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(registryJson);
        }
        catch (JsonException ex)
        {
            errors.Add($"Registry is not valid JSON: {ex.Message}");
            return;
        }

        using (doc)
        {
            var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
            var result = Schema.Value.Evaluate(doc.RootElement, options);

            if (result.IsValid)
            {
                return;
            }

            var details = result.Details ?? [];
            var any = false;

            foreach (var detail in details)
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
    }

    private static void ValidateCrossReferences(McpWorkersCatalog registry, List<string> errors)
    {
        var workerNames = new HashSet<string>(StringComparer.Ordinal);
        var workerIds = new HashSet<string>(StringComparer.Ordinal);
        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!int.TryParse(registry.SchemaVersion, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            errors.Add(
                $"Registry schemaVersion '{registry.SchemaVersion}' is not a non-negative integer.");
        }

        if (registry.Workers.Count == 0)
        {
            errors.Add("Registry contains no workers; at least one worker entry is required.");
        }

        for (var workerIndex = 0; workerIndex < registry.Workers.Count; workerIndex++)
        {
            var worker = registry.Workers[workerIndex];
            var workerPath = $"workers[{workerIndex}] ('{worker.Name}')";

            if (!workerNames.Add(worker.Name))
            {
                errors.Add($"Duplicate worker name '{worker.Name}' at {workerPath}.");
            }

            if (!workerIds.Add(worker.Id))
            {
                errors.Add($"Duplicate worker id '{worker.Id}' at {workerPath}.");
            }

            ValidateWorker(workerPath, worker, toolNames, errors);
        }
    }

    private static void ValidateWorker(
        string workerPath,
        McpWorker worker,
        Dictionary<string, string> toolNames,
        List<string> errors)
    {
        if (worker.Tools.Count == 0)
        {
            errors.Add($"Worker at {workerPath} declares no tools; at least one tool is required.");
        }

        for (var definitionIndex = 0; definitionIndex < worker.Tools.Count; definitionIndex++)
        {
            var tool = worker.Tools[definitionIndex];
            var toolPath = $"{workerPath}.tools[{definitionIndex}] ('{tool.Name}')";

            if (toolNames.TryGetValue(tool.Name, out var existingPath))
            {
                errors.Add(
                    $"Duplicate tool name '{tool.Name}' at {toolPath}; previously defined at {existingPath}.");
            }
            else
            {
                toolNames[tool.Name] = toolPath;
            }

            if (tool.Tasks.Count == 0)
            {
                errors.Add($"Tool {toolPath} declares no tasks; at least one task is required.");
            }

            var declaredTaskNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var task in tool.Tasks)
            {
                if (!declaredTaskNames.Add(task.Name))
                {
                    errors.Add($"Duplicate task name '{task.Name}' in tool {toolPath}.");
                }
            }
        }
    }

    private static JsonSchema LoadEmbeddedSchema()
    {
        var assembly = typeof(RegistrySchemeValidator).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedSchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedSchemaResourceName}' not found in {assembly.FullName}.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return JsonSchema.FromText(text);
    }
}
