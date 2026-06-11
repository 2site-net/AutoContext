namespace AutoContext.Engine.Core.Features.Instructions.Format;

/// <summary>
/// One category definition of the <c>instructions-catalog.json</c>
/// taxonomy: the bucket <see cref="Name"/> a file's membership references
/// and the human-readable <see cref="Description"/>. Mirrors the
/// generator's <c>JsonInstructionsCatalogCategory</c> shape.
/// </summary>
/// <param name="Name">The category name; the value a
/// <see cref="JsonInstructionsCatalogEntry.Categories"/> membership
/// resolves against.</param>
/// <param name="Description">The human-readable category
/// description.</param>
internal sealed record JsonInstructionsCatalogCategory(
    string? Name = null,
    string? Description = null);
