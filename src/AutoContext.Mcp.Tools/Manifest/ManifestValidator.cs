namespace AutoContext.Mcp.Tools.Manifest;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Json.Schema;

/// <summary>
/// Validates a parsed <see cref="Manifest"/> (and its raw JSON form) against
/// the embedded <c>.mcp-tools.schema.json</c> and a set of fail-fast
/// cross-reference rules.
/// </summary>
/// <remarks>
/// Detected failure modes:
/// <list type="bullet">
///   <item><description><b>Schema</b>: manifest doesn't conform to the JSON Schema.</description></item>
///   <item><description><b>Duplicate tool name</b>: two MCP Tools share a <c>definition.name</c> anywhere in the manifest.</description></item>
///   <item><description><b>Duplicate task name</b>: two MCP Tasks share a <c>name</c> within the same group.</description></item>
///   <item><description><b>Orphan task reference</b>: a tool's <c>tasks[]</c> entry doesn't match any task in the sibling <c>tasks[]</c>.</description></item>
///   <item><description><b>Unreferenced task</b>: a task declared in <c>tasks[]</c> that no tool in the same group references.</description></item>
/// </list>
/// </remarks>
public static class ManifestValidator
{
    private const string EmbeddedSchemaResourceName = "AutoContext.Mcp.Tools.mcp-tools.schema.json";

    private static readonly Lazy<JsonSchema> Schema = new(LoadEmbeddedSchema, isThreadSafe: true);

    /// <summary>
    /// Validates the supplied manifest JSON against the schema and the cross-reference rules.
    /// </summary>
    /// <param name="manifestJson">Raw JSON text for the manifest.</param>
    /// <param name="manifest">Parsed manifest.</param>
    public static ManifestValidationResult Validate(string manifestJson, Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifestJson);
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<string>();

        ValidateAgainstSchema(manifestJson, errors);
        ValidateCrossReferences(manifest, errors);

        return new ManifestValidationResult(errors);
    }

    private static void ValidateAgainstSchema(string manifestJson, List<string> errors)
    {
        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(manifestJson);
        }
        catch (JsonException ex)
        {
            errors.Add($"Manifest is not valid JSON: {ex.Message}");
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
                errors.Add("Manifest failed JSON Schema validation (no further detail).");
            }
        }
    }

    private static void ValidateCrossReferences(Manifest manifest, List<string> errors)
    {
        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (workerName, groups) in manifest.Workers)
        {
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                ValidateGroup(workerName, groupIndex, groups[groupIndex], toolNames, errors);
            }
        }
    }

    private static void ValidateGroup(
        string workerName,
        int groupIndex,
        ManifestGroup group,
        Dictionary<string, string> toolNames,
        List<string> errors)
    {
        var groupPath = $"{workerName}[{groupIndex}] ('{group.Tag}')";

        var declaredTaskNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in group.Tasks)
        {
            if (!declaredTaskNames.Add(task.Name))
            {
                errors.Add($"Duplicate task name '{task.Name}' in group {groupPath}.");
            }
        }

        var referencedTaskNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tool in group.Tools)
        {
            var toolName = tool.Definition.Name;

            if (toolNames.TryGetValue(toolName, out var existingPath))
            {
                errors.Add(
                    $"Duplicate tool name '{toolName}' in group {groupPath}; previously defined in {existingPath}.");
            }
            else
            {
                toolNames[toolName] = groupPath;
            }

            foreach (var taskRef in tool.Tasks)
            {
                referencedTaskNames.Add(taskRef);

                if (!declaredTaskNames.Contains(taskRef))
                {
                    errors.Add(
                        $"Tool '{toolName}' in group {groupPath} references unknown task '{taskRef}'.");
                }
            }
        }

        foreach (var task in group.Tasks)
        {
            if (!referencedTaskNames.Contains(task.Name))
            {
                errors.Add($"Task '{task.Name}' in group {groupPath} is declared but not referenced by any tool.");
            }
        }
    }

    private static JsonSchema LoadEmbeddedSchema()
    {
        var assembly = typeof(ManifestValidator).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedSchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedSchemaResourceName}' not found in {assembly.FullName}.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return JsonSchema.FromText(text);
    }
}
