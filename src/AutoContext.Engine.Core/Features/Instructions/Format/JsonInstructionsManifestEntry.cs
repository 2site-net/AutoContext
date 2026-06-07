namespace AutoContext.Engine.Core.Features.Instructions.Format;

/// <summary>
/// One row of the generated <c>instructions-manifest.json</c> fact
/// index: the machine-extracted facts for a single corpus file. Mirrors
/// the generator's <c>JsonInstructionsManifestEntry</c> writer. Carries
/// the <see cref="Sections"/> heading map and the <see cref="Extensions"/>
/// set the coarse <c>applyTo</c> filter intersects against, but no
/// curatorial data — label, categories, and always-attached membership
/// are the catalog's concern.
/// </summary>
/// <param name="Key">The stable key (the file basename without the
/// <c>.instructions.md</c> extension).</param>
/// <param name="FileName">The corpus file name including the
/// <c>.instructions.md</c> extension.</param>
/// <param name="Name">The raw frontmatter <c>name</c>
/// (<c>&lt;key&gt; (vX.Y.Z)</c>).</param>
/// <param name="Version">The semantic version extracted from
/// <see cref="Name"/>.</param>
/// <param name="Description">The trimmed frontmatter description.</param>
/// <param name="ApplyTo">The verbatim <c>applyTo</c> glob string, or
/// <see langword="null"/>.</param>
/// <param name="Extensions">The dotless extension set derived from
/// <see cref="ApplyTo"/>; <see langword="null"/> when no <c>applyTo</c>
/// is declared, and empty when <c>applyTo</c> names no concrete
/// extension.</param>
/// <param name="HasChangelog">Whether a sibling changelog file
/// exists.</param>
/// <param name="ContentHash">The content hash
/// (<c>sha256:&lt;hex&gt;</c>) of the body.</param>
/// <param name="Sections">The <c>##</c>/<c>###</c> section index, in
/// document order, or <see langword="null"/> when absent.</param>
internal sealed record JsonInstructionsManifestEntry(
    string? Key = null,
    string? FileName = null,
    string? Name = null,
    string? Version = null,
    string? Description = null,
    string? ApplyTo = null,
    IReadOnlyList<string>? Extensions = null,
    bool HasChangelog = false,
    string? ContentHash = null,
    IReadOnlyList<JsonInstructionsManifestSection>? Sections = null);
