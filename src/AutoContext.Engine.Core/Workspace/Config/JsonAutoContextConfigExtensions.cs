namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Format;

/// <summary>
/// Maps the on-disk <see cref="JsonAutoContextConfig"/> wire shape onto
/// the immutable <see cref="AutoContextConfig"/> domain graph. The wire
/// quirks decoded here — the shorthand <c>mcpTools: { "tool": false }</c>
/// versus the object form, and the disabled-only encoding of rules and
/// tasks — are kept out of the domain graph.
/// </summary>
internal static class JsonAutoContextConfigExtensions
{
    /// <summary>
    /// Builds the domain graph from a parsed wire config. Each rule and
    /// task present on disk is, by the format's design, a disabled one,
    /// so it maps to <c>Disabled = true</c>.
    /// </summary>
    /// <param name="json">The parsed wire config.</param>
    /// <returns>The equivalent domain snapshot.</returns>
    public static AutoContextConfig ToDomain(this JsonAutoContextConfig json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return new AutoContextConfig
        {
            Version = json.Version,
            Diagnostic = json.Diagnostic is { } diagnostic
                ? new DiagnosticConfig { WarnOnMissingId = diagnostic.WarnOnMissingId }
                : null,
            Instructions = MapInstructionsToDomain(json.Instructions),
            McpTools = MapToolsToDomain(json.McpTools),
        };
    }

    private static InstructionsFileConfig[] MapInstructionsToDomain(
        IReadOnlyDictionary<string, JsonInstructionsFileConfigEntry>? instructions)
    {
        if (instructions is null || instructions.Count == 0)
        {
            return [];
        }

        var result = new List<InstructionsFileConfig>(instructions.Count);

        foreach (var (name, entry) in instructions)
        {
            var rules = entry.DisabledInstructions is { Count: > 0 } ids
                ? ids.Select(id => new InstructionsFileConfig.InstructionsRule { Id = id, Disabled = true }).ToArray()
                : [];

            result.Add(new InstructionsFileConfig
            {
                Name = name,
                Disabled = entry.Enabled is false ? true : null,
                Version = entry.Version,
                Rules = rules,
            });
        }

        return [.. result];
    }

    private static McpToolConfig[] MapToolsToDomain(
        IReadOnlyDictionary<string, JsonMcpToolConfigValue>? tools)
    {
        if (tools is null || tools.Count == 0)
        {
            return [];
        }

        var result = new List<McpToolConfig>(tools.Count);

        foreach (var (name, value) in tools)
        {
            if (value.Entry is not { } entry)
            {
                result.Add(new McpToolConfig { Name = name, Disabled = true });
                continue;
            }

            var tasks = entry.DisabledTasks is { Count: > 0 } names
                ? names.Select(task => new McpToolConfig.McpTask { Name = task, Disabled = true }).ToArray()
                : [];

            result.Add(new McpToolConfig
            {
                Name = name,
                Disabled = entry.Enabled is false ? true : null,
                Version = entry.Version,
                Tasks = tasks,
            });
        }

        return [.. result];
    }
}
