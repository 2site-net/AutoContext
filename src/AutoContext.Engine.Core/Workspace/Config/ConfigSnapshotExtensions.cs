namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Format;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Config;

/// <summary>
/// Maps the immutable <see cref="ConfigSnapshot"/> domain graph onto
/// the two wire shapes it crosses to: the on-disk
/// <see cref="JsonConfigFile"/> format (via <see cref="ToFileFormat"/>),
/// whose space-saving quirks — dropping entries that carry no state,
/// choosing the shorthand <c>mcpTools: { "tool": false }</c> versus
/// the object form, and the disabled-only encoding of rules and tasks
/// — are kept out of the domain graph; and the
/// <see cref="JsonConfigSnapshot"/> Protocol shape (via
/// <see cref="ToWireFormat"/>) returned by the <c>Config.Get</c> RPC,
/// which is a structural, lossless one-for-one projection.
/// </summary>
internal static class ConfigSnapshotExtensions
{
    /// <summary>
    /// Builds the wire config from a domain graph, dropping entries that
    /// carry no state and choosing the shorthand or object form for each
    /// MCP tool. The top-level <c>version</c> is left as-is; the file
    /// writer stamps the engine version on save.
    /// </summary>
    /// <param name="config">The domain snapshot.</param>
    /// <returns>The equivalent wire config.</returns>
    public static JsonConfigFile ToFileFormat(this ConfigSnapshot config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new JsonConfigFile
        {
            Version = config.Version,
            Diagnostic = config.Diagnostic is { } diagnostic
                ? new JsonConfigFileDiagnostic(diagnostic.WarnOnMissingId)
                : null,
            Instructions = ToFileFormat(config.Instructions),
            McpTools = ToFileFormat(config.McpTools),
        };
    }

    /// <summary>
    /// Projects a domain snapshot onto its wire representation.
    /// </summary>
    /// <param name="config">The domain snapshot. Must not be
    /// <see langword="null"/>.</param>
    /// <returns>The equivalent <see cref="JsonConfigSnapshot"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/>
    /// is <see langword="null"/>.</exception>
    public static JsonConfigSnapshot ToWireFormat(this ConfigSnapshot config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new JsonConfigSnapshot
        {
            Version = config.Version,
            Diagnostic = config.Diagnostic is { } diagnostic
                ? new JsonConfigDiagnostic { WarnOnMissingId = diagnostic.WarnOnMissingId }
                : null,
            Instructions = [.. config.Instructions.Select(ToWireFormat)],
            McpTools = [.. config.McpTools.Select(ToWireFormat)],
        };
    }

    private static Dictionary<string, JsonConfigFileInstructionsEntry>? ToFileFormat(
        ConfigInstructionsFile[] instructions)
    {
        var result = new Dictionary<string, JsonConfigFileInstructionsEntry>(StringComparer.Ordinal);

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

            result[name] = new JsonConfigFileInstructionsEntry(
                Version: file.Version,
                Enabled: isDisabled ? false : null,
                DisabledInstructions: disabledRules.Count > 0 ? disabledRules : null);
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, JsonConfigFileMcpToolValue>? ToFileFormat(ConfigMcpTool[] tools)
    {
        var result = new Dictionary<string, JsonConfigFileMcpToolValue>(StringComparer.Ordinal);

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
                ? JsonConfigFileMcpToolValue.Disabled
                : JsonConfigFileMcpToolValue.FromEntry(new JsonConfigFileMcpToolEntry(
                    Enabled: isDisabled ? false : null,
                    Version: tool.Version,
                    DisabledTasks: hasTasks ? disabledTasks : null));
        }

        return result.Count == 0 ? null : result;
    }

    private static JsonConfigInstructionsFile ToWireFormat(ConfigInstructionsFile file)
        => new()
        {
            Name = file.Name,
            Version = file.Version,
            Disabled = file.Disabled,
            Rules =
            [
                .. file.Rules.Select(rule => new JsonConfigInstructionsRule
                {
                    Id = rule.Id,
                    Disabled = rule.Disabled,
                }),
            ],
        };

    private static JsonConfigMcpTool ToWireFormat(ConfigMcpTool tool)
        => new()
        {
            Name = tool.Name,
            Version = tool.Version,
            Disabled = tool.Disabled,
            Tasks =
            [
                .. tool.Tasks.Select(task => new JsonConfigMcpTask
                {
                    Name = task.Name,
                    Disabled = task.Disabled,
                }),
            ],
        };
}
