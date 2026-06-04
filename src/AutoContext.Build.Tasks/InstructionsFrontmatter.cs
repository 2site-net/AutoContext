namespace AutoContext.Build.Tasks;

/// <summary>
/// The subset of an instruction file's YAML frontmatter the wire-manifest
/// build pass reads. Every field is optional at the parse layer; the builder
/// validates presence and shape per file.
/// </summary>
internal sealed class InstructionsFrontmatter(string? name, string? description, string? applyTo)
{
    /// <summary>Gets the raw <c>applyTo</c> field, or <see langword="null"/> when absent.</summary>
    public string? ApplyTo { get; } = applyTo;

    /// <summary>Gets the raw <c>description</c> field.</summary>
    public string? Description { get; } = description;

    /// <summary>Gets the raw <c>name</c> field (expected <c>&lt;key&gt; (vX.Y.Z)</c>).</summary>
    public string? Name { get; } = name;
}
