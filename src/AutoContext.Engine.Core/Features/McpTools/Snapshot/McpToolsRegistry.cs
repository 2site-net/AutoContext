namespace AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Immutable snapshot of the bundled MCP-tools registry: the flat list of
/// <see cref="McpToolsRegistryTool"/> the engine advertises, in document
/// order, with an ordinal lookup by tool name. Built once at startup by
/// <see cref="McpToolsRegistryLoader"/> from the validated
/// <c>mcp-tools-registry.json</c> side-car. A reader holds the reference
/// it observed and is never mutated, so iteration is lock-free and never
/// tears.
/// </summary>
internal sealed class McpToolsRegistry
{
    /// <summary>
    /// The shared empty registry: no tools. The value to publish before a
    /// startup load completes.
    /// </summary>
    public static McpToolsRegistry Empty { get; } = new([]);

    private readonly Dictionary<string, McpToolsRegistryTool> _byName;

    /// <summary>
    /// Creates a snapshot over <paramref name="tools"/>, building the
    /// ordinal name lookup.
    /// </summary>
    /// <param name="tools">The tool definitions, in document order. Must
    /// not be <see langword="null"/>, contain a <see langword="null"/>
    /// element, or contain two tools with the same
    /// <see cref="McpToolsRegistryTool.Name"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tools"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tools"/>
    /// contains a <see langword="null"/> element or a duplicate tool
    /// name.</exception>
    public McpToolsRegistry(IReadOnlyList<McpToolsRegistryTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        _byName = new Dictionary<string, McpToolsRegistryTool>(
            tools.Count, StringComparer.Ordinal);

        foreach (var tool in tools)
        {
            if (tool is null)
            {
                throw new ArgumentException(
                    "MCP tool registry contains a null tool.", nameof(tools));
            }

            if (!_byName.TryAdd(tool.Name, tool))
            {
                throw new ArgumentException(
                    $"Duplicate MCP tool name '{tool.Name}' in registry.",
                    nameof(tools));
            }
        }

        Tools = tools;
    }

    /// <summary>The tool definitions in document order.</summary>
    public IReadOnlyList<McpToolsRegistryTool> Tools { get; }

    /// <summary>
    /// Returns the tool whose <see cref="McpToolsRegistryTool.Name"/>
    /// equals <paramref name="name"/> (ordinal), or <see langword="null"/>
    /// when no tool matches.
    /// </summary>
    /// <param name="name">The MCP tool name to look up. Must not be
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is
    /// <see langword="null"/>.</exception>
    public McpToolsRegistryTool? FindByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _byName.GetValueOrDefault(name);
    }
}
