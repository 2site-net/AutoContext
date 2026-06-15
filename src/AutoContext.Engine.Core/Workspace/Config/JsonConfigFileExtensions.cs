namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Format;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Maps the on-disk <see cref="JsonConfigFile"/> wire shape onto
/// the immutable <see cref="ConfigSnapshot"/> domain graph. The wire
/// quirk decoded here — the disabled-only encoding of rules and tools
/// — is kept out of the domain graph.
/// </summary>
internal static class JsonConfigFileExtensions
{
    /// <summary>
    /// Builds the domain graph from a parsed wire config. Each rule and
    /// tool present on disk is, by the format's design, a disabled one,
    /// so it maps to <c>Disabled = true</c>.
    /// </summary>
    /// <param name="json">The parsed wire config.</param>
    /// <returns>The equivalent domain snapshot.</returns>
    public static ConfigSnapshot ToDomainGraph(this JsonConfigFile json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return new ConfigSnapshot
        {
            Version = json.Version,
            Diagnostic = json.Diagnostic is { } diagnostic
                ? new ConfigDiagnostic { WarnOnMissingId = diagnostic.WarnOnMissingId }
                : null,
            Engine = json.Engine is { } engine
                ? new ConfigEngineSettings { InstructionsOverridesRoots = engine.InstructionsOverridesRoots ?? [] }
                : null,
            Instructions = ToDomainModel(json.Instructions),
            McpTools = ToDomainModel(json.McpTools),
        };
    }

    private static ConfigInstructionsFile[] ToDomainModel(
        IReadOnlyDictionary<string, JsonConfigFileInstructionsEntry>? instructions)
    {
        if (instructions is null || instructions.Count == 0)
        {
            return [];
        }

        var result = new List<ConfigInstructionsFile>(instructions.Count);

        foreach (var (name, entry) in instructions)
        {
            var rules = entry.DisabledRules is { Count: > 0 } ids
                ? ids.Select(id => new ConfigInstructionsFile.InstructionsRule { Id = id, Disabled = true }).ToArray()
                : [];

            result.Add(new ConfigInstructionsFile
            {
                Name = name,
                Disabled = entry.Disabled is true ? true : null,
                Version = entry.Version,
                Rules = rules,
            });
        }

        return [.. result];
    }

    private static ConfigMcpTool[] ToDomainModel(
        IReadOnlyDictionary<string, JsonConfigFileMcpToolEntry>? tools)
    {
        if (tools is null || tools.Count == 0)
        {
            return [];
        }

        var result = new List<ConfigMcpTool>(tools.Count);

        foreach (var (name, entry) in tools)
        {
            result.Add(new ConfigMcpTool
            {
                Name = name,
                Disabled = entry.Disabled is true ? true : null,
                Version = entry.Version,
            });
        }

        return [.. result];
    }
}
