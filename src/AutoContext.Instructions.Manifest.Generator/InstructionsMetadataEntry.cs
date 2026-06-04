namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One row of the catalogue-only <c>instructions-files-metadata.json</c> index.
/// It mirrors the wire-shape <see cref="InstructionsManifestEntry"/> for the
/// fields both share, but omits <c>alwaysAttached</c> (a wire concern) and adds
/// the engine-internal indices the wire shape must not leak: the
/// <see cref="Sections"/> heading map and the parsed <see cref="Extensions"/>
/// set the coarse <c>applyTo</c> filter intersects against. The metadata file
/// backs the language-model tools that read instruction bodies and section
/// anchors, not the engine's <c>Instructions.List</c> envelope.
/// </summary>
internal sealed class InstructionsMetadataEntry(
    string key,
    string fileName,
    string name,
    string version,
    string description,
    string? applyTo,
    IReadOnlyList<string>? extensions,
    bool hasChangelog,
    string contentHash,
    IReadOnlyList<InstructionsMetadataSection> sections)
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
    public IReadOnlyList<InstructionsMetadataSection> Sections { get; } = sections;

    /// <summary>Gets the semantic version extracted from <see cref="Name"/>.</summary>
    [JsonPropertyOrder(3)]
    public string Version { get; } = version;
}
