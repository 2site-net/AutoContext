namespace AutoContext.Engine.Core.Features.McpTools.Format;

/// <summary>
/// Disk read-model for one entry of the <c>categories</c> array in
/// <c>mcp-tools-catalog.json</c> — a single presentation category in the
/// taxonomy tree. Mirrors the <c>category</c> shape in
/// <c>mcp-tools-catalog.schema.json</c>: the <see cref="Name"/> tools and
/// child categories reference by value, the optional <see cref="Parent"/>
/// link, the human-facing <see cref="Description"/>, and the
/// <see cref="WorkerId"/> and <see cref="ActivationFlags"/> that
/// descendants inherit.
/// </summary>
/// <param name="Name">The category name; unique across the catalog.</param>
/// <param name="Parent">The parent category name; <see langword="null"/>
/// for a root category.</param>
/// <param name="Description">The human-facing category description.</param>
/// <param name="WorkerId">The worker identifier (kebab-case) descendants
/// inherit from the nearest ancestor that defines it, or
/// <see langword="null"/> when undefined at this level.</param>
/// <param name="ActivationFlags">The workspace-state flags (camelCase)
/// this category contributes to its subtree, or <see langword="null"/>
/// when it declares none.</param>
internal sealed record JsonMcpToolsCatalogCategory(
    string? Name = null,
    string? Parent = null,
    string? Description = null,
    string? WorkerId = null,
    IReadOnlyList<string>? ActivationFlags = null);
