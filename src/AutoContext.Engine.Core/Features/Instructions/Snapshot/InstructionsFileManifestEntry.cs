namespace AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// One entry in the in-memory instructions manifest: the merged metadata
/// describing a single bundled instructions file, composed at startup by
/// merging the <c>instructions-manifest.json</c>
/// fact row (identity, <see cref="Sections"/>, <see cref="Extensions"/>)
/// with its <c>instructions-catalog.json</c> curatorial row
/// (<see cref="Label"/>, <see cref="Categories"/>,
/// <see cref="ActivationFlags"/>) and deriving
/// <see cref="AlwaysAttached"/> from the catalog's always-attached list.
/// Pure data — it carries no body; the body is projected per request in
/// a later phase.
/// </summary>
internal sealed record InstructionsFileManifestEntry
{
    /// <summary>
    /// The stable key (the file basename without the
    /// <c>.instructions.md</c> extension).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The corpus file name including the <c>.instructions.md</c>
    /// extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The raw frontmatter <c>name</c> (<c>&lt;key&gt; (vX.Y.Z)</c>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The semantic version extracted from <see cref="Name"/>.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The trimmed frontmatter description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The verbatim <c>applyTo</c> glob string, or
    /// <see langword="null"/>.
    /// </summary>
    public string? ApplyTo { get; init; }

    /// <summary>
    /// The dotless extension set derived from <see cref="ApplyTo"/>;
    /// <see langword="null"/> when no <c>applyTo</c> is declared, and an
    /// empty list when <c>applyTo</c> names no concrete extension.
    /// </summary>
    public IReadOnlyList<string>? Extensions { get; init; }

    /// <summary>
    /// <see langword="true"/> when a sibling changelog file exists.
    /// </summary>
    public required bool HasChangelog { get; init; }

    /// <summary>
    /// The content hash (<c>sha256:&lt;hex&gt;</c>) of the body.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// <see langword="true"/> when the file is always attached, derived
    /// from membership in the catalog's always-attached list. Always-
    /// attached files carry no <see cref="Label"/> or
    /// <see cref="Categories"/>.
    /// </summary>
    public required bool AlwaysAttached { get; init; }

    /// <summary>
    /// The human-friendly catalog display label, or
    /// <see langword="null"/> for always-attached files (which the
    /// catalog deliberately omits).
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The category names this file belongs to; empty for always-
    /// attached files.
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// The engine-internal workspace-context flags that gate activation.
    /// Read by the workspace-context evaluator and never serialized to
    /// the wire; empty when the file is unconditional.
    /// </summary>
    public IReadOnlyList<string> ActivationFlags { get; init; } = [];

    /// <summary>
    /// The <c>##</c>/<c>###</c> section index, in document order.
    /// </summary>
    public IReadOnlyList<InstructionsSection> Sections { get; init; } = [];
}
