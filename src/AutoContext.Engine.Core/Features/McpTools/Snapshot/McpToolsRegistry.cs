namespace AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Immutable snapshot of the bundled MCP-tools surface: the
/// <see cref="Categories"/> presentation taxonomy and the flat list of
/// <see cref="McpToolsRegistryEntry"/> the engine advertises, in document
/// order, with an ordinal lookup by tool name. Built once at startup by
/// <see cref="McpToolsRegistryLoader"/> by merging the validated
/// <c>mcp-tools-registry.json</c> and <c>mcp-tools-catalog.json</c>
/// side-cars into a single source of truth. A reader holds the reference
/// it observed and is never mutated, so iteration is lock-free and never
/// tears.
/// </summary>
internal sealed class McpToolsRegistry
{
    /// <summary>
    /// The shared empty registry: no categories, no tools. The value to
    /// publish before a startup load completes.
    /// </summary>
    public static McpToolsRegistry Empty { get; } = new([], []);

    private readonly Dictionary<string, McpToolsRegistryEntry> _byName;

    /// <summary>
    /// Creates a snapshot over <paramref name="categories"/> and
    /// <paramref name="tools"/>, building the ordinal name lookup.
    /// </summary>
    /// <param name="categories">The presentation categories, in document
    /// order. Must not be <see langword="null"/>, contain a
    /// <see langword="null"/> element, or contain two categories with the
    /// same <see cref="McpToolsCategoryEntry.Name"/>.</param>
    /// <param name="tools">The tool definitions, in document order. Must
    /// not be <see langword="null"/>, contain a <see langword="null"/>
    /// element, or contain two tools with the same
    /// <see cref="McpToolsRegistryEntry.Name"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="categories"/> or <paramref name="tools"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="categories"/> or
    /// <paramref name="tools"/> contains a <see langword="null"/> element
    /// or a duplicate name.</exception>
    public McpToolsRegistry(
        IReadOnlyList<McpToolsCategoryEntry> categories,
        IReadOnlyList<McpToolsRegistryEntry> tools)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(tools);

        var categoryNames = new HashSet<string>(categories.Count, StringComparer.Ordinal);

        foreach (var category in categories)
        {
            if (category is null)
            {
                throw new ArgumentException(
                    "MCP tools registry contains a null category.", nameof(categories));
            }

            if (!categoryNames.Add(category.Name))
            {
                throw new ArgumentException(
                    $"Duplicate MCP tools category name '{category.Name}' in registry.",
                    nameof(categories));
            }
        }

        _byName = new Dictionary<string, McpToolsRegistryEntry>(
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

        Categories = categories;
        Tools = tools;
    }

    /// <summary>The presentation categories in document order.</summary>
    public IReadOnlyList<McpToolsCategoryEntry> Categories { get; }

    /// <summary>The tool definitions in document order.</summary>
    public IReadOnlyList<McpToolsRegistryEntry> Tools { get; }

    /// <summary>
    /// Returns the tool whose <see cref="McpToolsRegistryEntry.Name"/>
    /// equals <paramref name="name"/> (ordinal), or <see langword="null"/>
    /// when no tool matches.
    /// </summary>
    /// <param name="name">The MCP tool name to look up. Must not be
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is
    /// <see langword="null"/>.</exception>
    public McpToolsRegistryEntry? FindByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _byName.GetValueOrDefault(name);
    }
}
