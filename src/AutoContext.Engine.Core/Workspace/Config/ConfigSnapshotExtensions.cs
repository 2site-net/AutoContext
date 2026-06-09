namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Format;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Config;

/// <summary>
/// Maps the immutable <see cref="ConfigSnapshot"/> domain graph onto
/// the two wire shapes it crosses to: the on-disk
/// <see cref="JsonConfigFile"/> format (via <see cref="ToFileFormat"/>),
/// whose space-saving quirks — dropping entries that carry no state and
/// the disabled-only encoding of rules and tasks — are kept out of the
/// domain graph; and the <see cref="JsonConfigSnapshot"/> Protocol shape
/// (via <see cref="ToWireFormat"/>) returned by the <c>Config.Get</c>
/// RPC, which is a structural, lossless one-for-one projection.
/// </summary>
internal static class ConfigSnapshotExtensions
{
    /// <summary>
    /// Builds the wire config from a domain graph, dropping entries that
    /// carry no state. The top-level <c>version</c> is left as-is; the
    /// file writer stamps the engine version on save.
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
            Engine = config.Engine is { InstructionsOverridesRoots.Count: > 0 } engine
                ? new JsonConfigFileEngine([.. engine.InstructionsOverridesRoots])
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

    /// <summary>
    /// Flips the whole-file disabled flag for the instruction file
    /// named <paramref name="name"/>. An untracked file becomes
    /// disabled; a disabled file becomes enabled (and is pruned when
    /// it then carries no rule state). The input snapshot is never
    /// mutated; toggling normalises the result the same way a reload
    /// would, dropping an entry that ends up carrying no state.
    /// </summary>
    /// <param name="snapshot">The domain snapshot to derive from.</param>
    /// <param name="name">The instruction file to toggle.</param>
    /// <returns>A new snapshot with the file's state flipped.</returns>
    public static ConfigSnapshot ToggleInstructionsFile(this ConfigSnapshot snapshot, string name)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var files = snapshot.Instructions;
        var index = Array.FindIndex(
            files, file => string.Equals(file.Name, name, StringComparison.Ordinal));

        if (index < 0)
        {
            var created = new ConfigInstructionsFile { Name = name, Disabled = true };
            return snapshot with { Instructions = [.. files, created] };
        }

        var existing = files[index];
        var toggled = existing with
        {
            Disabled = existing.Disabled is true ? null : true,
        };

        return snapshot with { Instructions = ReplaceInstructionsFile(files, index, toggled) };
    }

    /// <summary>
    /// Flips the disabled flag for the rule <paramref name="ruleId"/>
    /// within the instruction file named <paramref name="name"/>. A
    /// disabled rule is enabled by dropping its entry; any other rule
    /// is recorded as disabled. The owning file is created when absent
    /// and pruned when the edit leaves it with no state. The input
    /// snapshot is never mutated.
    /// </summary>
    /// <param name="snapshot">The domain snapshot to derive from.</param>
    /// <param name="name">The instruction file owning the rule.</param>
    /// <param name="ruleId">The rule to toggle.</param>
    /// <returns>A new snapshot with the rule's state flipped.</returns>
    public static ConfigSnapshot ToggleInstructionsRule(
        this ConfigSnapshot snapshot, string name, string ruleId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        var files = snapshot.Instructions;
        var index = Array.FindIndex(
            files, file => string.Equals(file.Name, name, StringComparison.Ordinal));

        if (index < 0)
        {
            var created = new ConfigInstructionsFile
            {
                Name = name,
                Rules = [new ConfigInstructionsFile.InstructionsRule { Id = ruleId, Disabled = true }],
            };
            return snapshot with { Instructions = [.. files, created] };
        }

        var file = files[index];
        var toggled = file with { Rules = UpdateInstructionsRuleState(file.Rules, ruleId) };

        return snapshot with { Instructions = ReplaceInstructionsFile(files, index, toggled) };
    }

    private static bool IsEmpty(ConfigInstructionsFile file)
        => file.Disabled is not true && file.Rules.Length == 0;

    private static ConfigInstructionsFile[] ReplaceInstructionsFile(
        ConfigInstructionsFile[] files, int index, ConfigInstructionsFile replacement)
    {
        if (IsEmpty(replacement))
        {
            return [.. files[..index], .. files[(index + 1)..]];
        }

        var next = (ConfigInstructionsFile[])files.Clone();
        next[index] = replacement;
        return next;
    }

    private static ConfigInstructionsFile.InstructionsRule[] ReplaceInstructionsRule(
        ConfigInstructionsFile.InstructionsRule[] rules,
        int index,
        ConfigInstructionsFile.InstructionsRule replacement)
    {
        var next = (ConfigInstructionsFile.InstructionsRule[])rules.Clone();
        next[index] = replacement;
        return next;
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
                Disabled: isDisabled ? true : null,
                DisabledRules: disabledRules.Count > 0 ? disabledRules : null);
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, JsonConfigFileMcpToolEntry>? ToFileFormat(ConfigMcpTool[] tools)
    {
        var result = new Dictionary<string, JsonConfigFileMcpToolEntry>(StringComparer.Ordinal);

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

            result[name] = new JsonConfigFileMcpToolEntry(
                Disabled: isDisabled ? true : null,
                Version: tool.Version,
                DisabledTasks: hasTasks ? disabledTasks : null);
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

    private static ConfigInstructionsFile.InstructionsRule[] UpdateInstructionsRuleState(
        ConfigInstructionsFile.InstructionsRule[] rules, string ruleId)
    {
        var index = Array.FindIndex(
            rules, rule => string.Equals(rule.Id, ruleId, StringComparison.Ordinal));

        if (index >= 0 && rules[index].Disabled is true)
        {
            // Enabling a disabled rule drops its entry entirely, so an
            // enabled rule never lingers as dead weight in the graph.
            return [.. rules[..index], .. rules[(index + 1)..]];
        }

        var disabled = new ConfigInstructionsFile.InstructionsRule { Id = ruleId, Disabled = true };

        return index >= 0
            ? ReplaceInstructionsRule(rules, index, disabled)
            : [.. rules, disabled];
    }
}
