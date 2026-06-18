namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One curated entry in the hand-authored <c>instructions-catalog.json</c>: the
/// presentation <see cref="Label"/>, the corpus <see cref="FileName"/> it decorates,
/// the <see cref="Category"/> it belongs to, and the optional
/// <see cref="ActivationFlags"/> that gate when the engine surfaces it. The entry
/// is keyed by <see cref="FileName"/> rather than a separate name field so it joins
/// directly to the generated <see cref="JsonInstructionsManifestEntry.FileName"/>.
/// </summary>
internal sealed class JsonInstructionsCatalogEntry(
    string label,
    string fileName,
    string category,
    IReadOnlyList<string>? activationFlags)
{
    /// <summary>Gets the workspace-state flags that gate activation, or <see langword="null"/> when always offered.</summary>
    [JsonPropertyOrder(3)]
    public IReadOnlyList<string>? ActivationFlags { get; } = activationFlags;

    /// <summary>Gets the category name this entry belongs to.</summary>
    [JsonPropertyOrder(2)]
    public string Category { get; } = category;

    /// <summary>Gets the corpus file name (the join key into the manifest).</summary>
    [JsonPropertyOrder(1)]
    public string FileName { get; } = fileName;

    /// <summary>Gets the human-readable presentation label.</summary>
    [JsonPropertyOrder(0)]
    public string Label { get; } = label;
}
