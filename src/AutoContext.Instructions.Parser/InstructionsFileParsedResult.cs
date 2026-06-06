namespace AutoContext.Instructions.Parser;

/// <summary>
/// The complete structural parse of one instructions file: its parsed frontmatter
/// paired with its parsed body (the normalised body text, the section index, the
/// actionable rule bullets, and any bullet-tag diagnostics). This is the single
/// shape every consumer — the build-time catalogue and metadata generators and the
/// runtime engine — reads, so the markdown is parsed once.
/// </summary>
/// <param name="Frontmatter">The parsed leading YAML frontmatter.</param>
/// <param name="Body">The parsed body: normalised text plus its section, rule, and
/// diagnostic index.</param>
public sealed record InstructionsFileParsedResult(
    InstructionsFileFrontmatterParsedResult Frontmatter,
    InstructionsFileBodyParsedResult Body);
