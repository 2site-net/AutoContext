namespace AutoContext.Engine.Core.Features.Discovery;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Discovery;

/// <summary>
/// Serves the <c>Discovery.*</c> routing surface over indices the engine
/// already owns. The bundled corpus and tool registry are immutable at
/// runtime, so the two structural indices
/// (<see cref="CategoryIndex"/>, <see cref="ExtensionIndex"/>) are built
/// lazily once — on the first query, after the startup loaders have
/// populated the accessors — while the disabled filter is read from
/// <see cref="IConfigSnapshotAccessor.Current"/> on every query, so
/// results always reflect the current <c>.autocontext.json</c> state
/// without any index rebuild.
/// </summary>
/// <remarks>
/// A P11 capability that composes the read-only
/// <see cref="IMcpToolsRegistryAccessor"/> and
/// <see cref="IInstructionsManifestAccessor"/> snapshots — a
/// capability-to-capability read, which the tier rule permits (only the
/// substrate may never depend on a capability).
/// </remarks>
internal sealed class DiscoveryService
{
    private readonly Lazy<CategoryIndex> _categoryIndex;
    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly Lazy<ExtensionIndex> _extensionIndex;
    private readonly IInstructionsManifestAccessor _manifestAccessor;
    private readonly IMcpToolsRegistryAccessor _registryAccessor;

    /// <summary>
    /// Creates the service over the read-only registry, manifest, and
    /// config seams.
    /// </summary>
    /// <param name="registryAccessor">Read seam over the immutable
    /// MCP-tools registry snapshot.</param>
    /// <param name="manifestAccessor">Read seam over the immutable
    /// instructions manifest snapshot.</param>
    /// <param name="configAccessor">Read seam over the workspace config,
    /// supplying the per-query disabled state.</param>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public DiscoveryService(
        IMcpToolsRegistryAccessor registryAccessor,
        IInstructionsManifestAccessor manifestAccessor,
        IConfigSnapshotAccessor configAccessor)
    {
        ArgumentNullException.ThrowIfNull(registryAccessor);
        ArgumentNullException.ThrowIfNull(manifestAccessor);
        ArgumentNullException.ThrowIfNull(configAccessor);

        _registryAccessor = registryAccessor;
        _manifestAccessor = manifestAccessor;
        _configAccessor = configAccessor;
        _categoryIndex = new Lazy<CategoryIndex>(() => new CategoryIndex(_registryAccessor.Current));
        _extensionIndex = new Lazy<ExtensionIndex>(() => new ExtensionIndex(_manifestAccessor.Current));
    }

    /// <summary>
    /// Routes <paramref name="prompt"/> to the strongly-relevant tools and
    /// instructions files, filtered by the current disabled state.
    /// </summary>
    /// <param name="prompt">The user prompt to route.</param>
    /// <returns>The matched categories and extensions plus the enabled
    /// tools and instructions files.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/>
    /// is <see langword="null"/>.</exception>
    public JsonDiscoveryRouteForPromptResult RouteForPrompt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var (categories, tools) = _categoryIndex.Value.Match(prompt);
        var (extensions, files) = _extensionIndex.Value.Match(prompt);
        var config = _configAccessor.Current;

        var enabledTools = new List<string>(tools.Count);

        foreach (var tool in tools)
        {
            if (!IsToolDisabled(config, tool))
            {
                enabledTools.Add(tool);
            }
        }

        var enabledInstructions = new List<string>(files.Count);

        foreach (var file in files)
        {
            if (!IsInstructionsFileDisabled(config, file.Key))
            {
                enabledInstructions.Add(file.FileName);
            }
        }

        return new JsonDiscoveryRouteForPromptResult
        {
            MatchedCategories = categories,
            MatchedExtensions = extensions,
            Tools = enabledTools,
            Instructions = enabledInstructions,
        };
    }

    /// <summary>
    /// Routes <paramref name="toolName"/> to the instructions files whose
    /// workspace-context activation flags intersect the tool's, filtered
    /// by the current disabled state. An unknown or flagless tool yields
    /// an empty route.
    /// </summary>
    /// <param name="toolName">The MCP tool name to route.</param>
    /// <returns>The domain-relevant, enabled instructions file names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="toolName"/>
    /// is <see langword="null"/>.</exception>
    public JsonDiscoveryRouteForToolResult RouteForTool(string toolName)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        var tool = _registryAccessor.Current.FindByName(toolName);

        if (tool is null || tool.ActivationFlags.Count == 0)
        {
            return new JsonDiscoveryRouteForToolResult();
        }

        var toolFlags = new HashSet<string>(tool.ActivationFlags, StringComparer.Ordinal);
        var config = _configAccessor.Current;
        var instructions = new List<string>();

        foreach (var file in _manifestAccessor.Current.Files)
        {
            if (!IntersectsAny(file.ActivationFlags, toolFlags))
            {
                continue;
            }

            if (IsInstructionsFileDisabled(config, file.Key))
            {
                continue;
            }

            instructions.Add(file.FileName);
        }

        return new JsonDiscoveryRouteForToolResult { Instructions = instructions };
    }

    private static bool IntersectsAny(IReadOnlyList<string> flags, HashSet<string> candidates)
    {
        foreach (var flag in flags)
        {
            if (candidates.Contains(flag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInstructionsFileDisabled(ConfigSnapshot config, string key)
        => Array.Find(
            config.Instructions,
            file => string.Equals(file.Name, key, StringComparison.Ordinal))?.Disabled == true;

    private static bool IsToolDisabled(ConfigSnapshot config, string name)
        => Array.Find(
            config.McpTools,
            tool => string.Equals(tool.Name, name, StringComparison.Ordinal))?.Disabled == true;
}
