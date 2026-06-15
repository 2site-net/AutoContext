namespace AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Immutable domain model for a single MCP-tools presentation category,
/// composed at startup by <see cref="McpToolsRegistryLoader"/> from one
/// <c>categories</c> entry of <c>mcp-tools-catalog.json</c>. Carries the
/// human-facing <see cref="Description"/>, the <see cref="Parent"/> link
/// that forms the taxonomy tree, and the <see cref="ActivationFlags"/>
/// flattened from this category's ancestry.
/// </summary>
internal sealed record McpToolsCategoryEntry
{
    /// <summary>
    /// The workspace-context flags that gate this category, flattened from
    /// its ancestry in root-to-leaf order (e.g. the C# category resolves to
    /// <c>["hasDotNet", "hasCSharp"]</c>); empty when unconditional.
    /// </summary>
    public IReadOnlyList<string> ActivationFlags { get; init; } = [];

    /// <summary>The human-facing category description.</summary>
    public required string Description { get; init; }

    /// <summary>The category name; unique across the catalog.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The parent category name, or <see langword="null"/> for a root
    /// category.
    /// </summary>
    public string? Parent { get; init; }
}
