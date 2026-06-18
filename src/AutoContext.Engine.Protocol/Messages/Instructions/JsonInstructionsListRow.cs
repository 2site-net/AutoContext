namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One identity row of the <see cref="InstructionsMethods.List"/>
/// listing. Carries every build-time manifest field, the curatorial
/// <see cref="Label"/> and category <see cref="Category"/> membership
/// from <c>instructions-catalog.json</c>, plus the four values the
/// engine resolves per request from workspace state
/// (<see cref="Disabled"/>, <see cref="Source"/>,
/// <see cref="OverridePath"/>, <see cref="Sections"/>). Bodies are
/// never included — the tree-view bulk render would otherwise pull
/// every body for nothing.
/// </summary>
public sealed record JsonInstructionsListRow
{
    /// <summary>File basename (e.g. <c>dotnet-async-await</c>).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>The corpus file name (<c>&lt;key&gt;.instructions.md</c>).</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    /// <summary>Raw frontmatter <c>name</c> (<c>&lt;key&gt; (vX.Y.Z)</c>).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Semantic version parsed from <see cref="Name"/>.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>Trimmed frontmatter description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Verbatim <c>applyTo</c> glob string, or <see langword="null"/>
    /// when the file declares none.
    /// </summary>
    [JsonPropertyName("applyTo")]
    public string? ApplyTo { get; init; }

    /// <summary>Whether a sibling <c>&lt;key&gt;.CHANGELOG.md</c> exists.</summary>
    [JsonPropertyName("hasChangelog")]
    public bool HasChangelog { get; init; }

    /// <summary>Content hash (<c>sha256:&lt;hex&gt;</c>) over the post-frontmatter body.</summary>
    [JsonPropertyName("contentHash")]
    public string? ContentHash { get; init; }

    /// <summary>Whether the catalog declares this file in its <c>alwaysAttached[]</c> array.</summary>
    [JsonPropertyName("alwaysAttached")]
    public bool AlwaysAttached { get; init; }

    /// <summary>
    /// Curatorial display label from <c>instructions-catalog.json</c>,
    /// or <see langword="null"/> when the catalog declares none (the
    /// client then falls back to <see cref="Key"/>).
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>
    /// Category-membership name from <c>instructions-catalog.json</c>;
    /// resolves to a <see cref="JsonInstructionsCategory"/>
    /// definition returned by <see cref="InstructionsMethods.Categories"/>.
    /// <see langword="null"/> when the file is uncategorized.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>
    /// Engine-resolved disabled state against <c>.autocontext.json</c>.
    /// Disabled rows still appear in <see cref="InstructionsMethods.List"/>
    /// so the tree view can render the toggle UI.
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    /// <summary>Whether the row resolved to the bundled or override file.</summary>
    [JsonPropertyName("source")]
    public InstructionsSource Source { get; init; }

    /// <summary>
    /// Workspace-relative path of the override, present only when
    /// <see cref="Source"/> is <see cref="InstructionsSource.Override"/>.
    /// </summary>
    [JsonPropertyName("overridePath")]
    public string? OverridePath { get; init; }

    /// <summary>
    /// Section index, present only when the request set
    /// <see cref="JsonInstructionsListParams.IncludeSections"/> (the
    /// default).
    /// </summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<JsonInstructionsSection>? Sections { get; init; }
}
