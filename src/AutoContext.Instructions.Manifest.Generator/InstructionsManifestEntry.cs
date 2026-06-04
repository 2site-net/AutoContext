namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One row of the wire-shape <c>instructions-files.json</c> catalogue — every
/// field the engine's <c>Instructions.List</c> envelope returns except those
/// resolved per request from workspace state (<c>disabled</c>, <c>source</c>,
/// <c>overridePath</c>, <c>sections</c>).
/// </summary>
internal sealed class InstructionsManifestEntry(
    string key,
    string fileName,
    string name,
    string version,
    string description,
    string? applyTo,
    bool hasChangelog,
    string contentHash,
    bool alwaysAttached)
{
    /// <summary>Gets a value indicating whether the file is always attached.</summary>
    [JsonPropertyOrder(8)]
    public bool AlwaysAttached { get; } = alwaysAttached;

    /// <summary>Gets the verbatim <c>applyTo</c> glob string, or <see langword="null"/>.</summary>
    [JsonPropertyOrder(5)]
    public string? ApplyTo { get; } = applyTo;

    /// <summary>Gets the content hash (<c>sha256:&lt;hex&gt;</c>) of the body.</summary>
    [JsonPropertyOrder(7)]
    public string ContentHash { get; } = contentHash;

    /// <summary>Gets the trimmed frontmatter description.</summary>
    [JsonPropertyOrder(4)]
    public string Description { get; } = description;

    /// <summary>Gets the corpus file name including <c>.instructions.md</c>.</summary>
    [JsonPropertyOrder(1)]
    public string FileName { get; } = fileName;

    /// <summary>Gets a value indicating whether a sibling changelog file exists.</summary>
    [JsonPropertyOrder(6)]
    public bool HasChangelog { get; } = hasChangelog;

    /// <summary>Gets the stable key (the file basename without the extension).</summary>
    [JsonPropertyOrder(0)]
    public string Key { get; } = key;

    /// <summary>Gets the raw frontmatter <c>name</c> (<c>&lt;key&gt; (vX.Y.Z)</c>).</summary>
    [JsonPropertyOrder(2)]
    public string Name { get; } = name;

    /// <summary>Gets the semantic version extracted from <see cref="Name"/>.</summary>
    [JsonPropertyOrder(3)]
    public string Version { get; } = version;
}
