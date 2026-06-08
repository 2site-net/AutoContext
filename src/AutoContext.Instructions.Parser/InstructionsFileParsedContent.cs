namespace AutoContext.Instructions.Parser;

/// <summary>
/// The full parse of one instructions file: its frontmatter together with its
/// body (the body text, the list of sections, the rule bullets, and any
/// diagnostics). Everything that reads instructions files — the build-time
/// manifest generator and the runtime engine — works from this one shape, so each
/// file is parsed just once.
/// </summary>
/// <param name="RawContent">The exact file content, frontmatter and body
/// included.</param>
/// <param name="Frontmatter">The parsed frontmatter from the top of the file.</param>
/// <param name="Body">The parsed body: the body text plus its sections, rules, and
/// diagnostics.</param>
public sealed record InstructionsFileParsedContent(
    string RawContent,
    InstructionsFileParsedFrontmatter Frontmatter,
    InstructionsFileParsedBody Body);
