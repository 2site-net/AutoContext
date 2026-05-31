namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Format;

/// <summary>
/// Maps the immutable <see cref="AutoContextConfig"/> domain graph onto
/// the on-disk <see cref="JsonAutoContextConfig"/> wire shape. The wire
/// quirks encoded here — dropping entries that carry no state, choosing
/// the shorthand <c>mcpTools: { "tool": false }</c> versus the object
/// form, and the disabled-only encoding of rules and tasks — are kept
/// out of the domain graph.
/// </summary>
internal static class AutoContextConfigExtensions
{
    /// <summary>
    /// Builds the wire config from a domain graph, dropping entries that
    /// carry no state and choosing the shorthand or object form for each
    /// MCP tool. The top-level <c>version</c> is left as-is; the file
    /// writer stamps the engine version on save.
    /// </summary>
    /// <param name="config">The domain snapshot.</param>
    /// <returns>The equivalent wire config.</returns>
    public static JsonAutoContextConfig ToJson(this AutoContextConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new JsonAutoContextConfig
        {
            Version = config.Version,
            Diagnostic = config.Diagnostic is { } diagnostic
                ? new JsonDiagnosticConfig(diagnostic.WarnOnMissingId)
                : null,
            Instructions = MapInstructionsToJson(config.Instructions),
            McpTools = MapToolsToJson(config.McpTools),
        };
    }

    private static Dictionary<string, JsonInstructionsFileConfigEntry>? MapInstructionsToJson(
        InstructionsFileConfig[] instructions)
    {
        var result = new Dictionary<string, JsonInstructionsFileConfigEntry>(StringComparer.Ordinal);

        foreach (var file in instructions)
        {
            if (file.Name is not { } name)
            {
                continue;
            }

            var disabledRules = file.Rules
                .Where(rule => rule.Disabled is true)
                .Select(rule => rule.Id)
                .OfType<string>()
                .ToList();

            var isDisabled = file.Disabled is true;

            if (!isDisabled && disabledRules.Count == 0)
            {
                continue;
            }

            result[name] = new JsonInstructionsFileConfigEntry(
                Version: file.Version,
                Enabled: isDisabled ? false : null,
                DisabledInstructions: disabledRules.Count > 0 ? disabledRules : null);
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, JsonMcpToolConfigValue>? MapToolsToJson(McpToolConfig[] tools)
    {
        var result = new Dictionary<string, JsonMcpToolConfigValue>(StringComparer.Ordinal);

        foreach (var tool in tools)
        {
            if (tool.Name is not { } name)
            {
                continue;
            }

            var disabledTasks = tool.Tasks
                .Where(task => task.Disabled is true)
                .Select(task => task.Name)
                .OfType<string>()
                .ToList();

            var isDisabled = tool.Disabled is true;
            var hasTasks = disabledTasks.Count > 0;

            if (!isDisabled && !hasTasks)
            {
                continue;
            }

            var hasVersion = tool.Version is not null;

            result[name] = isDisabled && !hasVersion && !hasTasks
                ? JsonMcpToolConfigValue.Disabled
                : JsonMcpToolConfigValue.FromEntry(new JsonMcpToolConfigEntry(
                    Enabled: isDisabled ? false : null,
                    Version: tool.Version,
                    DisabledTasks: hasTasks ? disabledTasks : null));
        }

        return result.Count == 0 ? null : result;
    }
}
