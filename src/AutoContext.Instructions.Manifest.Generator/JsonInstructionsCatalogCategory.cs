namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One category declared in the hand-authored <c>instructions-catalog.json</c>:
/// the presentation grouping (e.g. <c>Languages</c>, <c>.NET</c>, <c>Web</c>) a
/// catalog entry's <see cref="JsonInstructionsCatalogEntry.Category"/> membership
/// resolves against.
/// </summary>
internal sealed class JsonInstructionsCatalogCategory(
    string name,
    string description)
{
    /// <summary>Gets the human-readable category description.</summary>
    [JsonPropertyOrder(1)]
    public string Description { get; } = description;

    /// <summary>Gets the category name that entries reference by value.</summary>
    [JsonPropertyOrder(0)]
    public string Name { get; } = name;
}
