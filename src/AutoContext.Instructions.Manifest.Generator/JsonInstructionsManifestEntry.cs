namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One row of the build-generated <c>instructions-manifest.json</c>: the
/// machine-extracted facts about a single corpus file. It carries the parsed
/// frontmatter (name, version, description, verbatim <c>applyTo</c>), the derived
/// <see cref="Extensions"/> set the coarse <c>applyTo</c> filter intersects
/// against, the body <see cref="Sections"/> heading map, and the change-tracking
/// fields (<see cref="HasChangelog"/>, <see cref="ContentHash"/>). It deliberately
/// omits the curatorial fields — <c>label</c>, category membership, and
/// <c>activationFlags</c> live in the hand-authored
/// <see cref="JsonInstructionsCatalogEntry"/> — and the engine-derived
/// <c>alwaysAttached</c> flag, which the engine computes at runtime rather than
/// baking onto disk.
/// </summary>
internal sealed class JsonInstructionsManifestEntry(
    string key,
    string fileName,
    string name,
    string version,
    string description,
    string? applyTo,
    IReadOnlyList<string>? extensions,
    bool hasChangelog,
    string contentHash,
    IReadOnlyList<JsonInstructionsManifestSection> sections)
{
    /// <summary>Gets the verbatim <c>applyTo</c> glob string, or <see langword="null"/>.</summary>
    [JsonPropertyOrder(5)]
    public string? ApplyTo { get; } = applyTo;

    /// <summary>Gets the content hash (<c>sha256:&lt;hex&gt;</c>) of the body.</summary>
    [JsonPropertyOrder(8)]
    public string ContentHash { get; } = contentHash;

    /// <summary>Gets the trimmed frontmatter description.</summary>
    [JsonPropertyOrder(4)]
    public string Description { get; } = description;

    /// <summary>
    /// Gets the dotless extension set derived from <see cref="ApplyTo"/>, sorted
    /// ordinal; <see langword="null"/> when no <c>applyTo</c> is declared, and an
    /// empty list when <c>applyTo</c> names no concrete extension.
    /// </summary>
    [JsonPropertyOrder(6)]
    public IReadOnlyList<string>? Extensions { get; } = extensions;

    /// <summary>Gets the corpus file name including <c>.instructions.md</c>.</summary>
    [JsonPropertyOrder(1)]
    public string FileName { get; } = fileName;

    /// <summary>Gets a value indicating whether a sibling changelog file exists.</summary>
    [JsonPropertyOrder(7)]
    public bool HasChangelog { get; } = hasChangelog;

    /// <summary>Gets the stable key (the file basename without the extension).</summary>
    [JsonPropertyOrder(0)]
    public string Key { get; } = key;

    /// <summary>Gets the raw frontmatter <c>name</c> (<c>&lt;key&gt; (vX.Y.Z)</c>).</summary>
    [JsonPropertyOrder(2)]
    public string Name { get; } = name;

    /// <summary>Gets the <c>##</c>/<c>###</c> section index, in document order.</summary>
    [JsonPropertyOrder(9)]
    public IReadOnlyList<JsonInstructionsManifestSection> Sections { get; } = sections;

    /// <summary>Gets the semantic version extracted from <see cref="Name"/>.</summary>
    [JsonPropertyOrder(3)]
    public string Version { get; } = version;
}
