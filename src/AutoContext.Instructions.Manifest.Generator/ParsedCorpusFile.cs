namespace AutoContext.Instructions.Manifest.Generator;

using AutoContext.Instructions.Parser;

/// <summary>
/// One curated corpus file read from disk and parsed exactly once. The
/// <see cref="CorpusParser"/> bundles everything the downstream builders and the
/// reference validator need — its single
/// <see cref="InstructionsFileParser.Parse(string)"/> result (which carries the
/// verbatim file text), the precomputed frontmatter-stripped content hash, and
/// whether a sibling <c>.CHANGELOG.md</c> ships — so no consumer re-reads the file
/// or re-parses its markdown.
/// </summary>
/// <param name="FileName">The corpus file name (e.g. <c>testing.instructions.md</c>).</param>
/// <param name="Parsed">The single structural parse of the file, including its
/// verbatim <see cref="InstructionsFileParsedResult.RawContent"/>.</param>
/// <param name="ContentHash">The <c>sha256:&lt;hex&gt;</c> hash of the
/// frontmatter-stripped body.</param>
/// <param name="HasChangelog">Whether a sibling <c>&lt;key&gt;.CHANGELOG.md</c> file exists.</param>
internal sealed record ParsedCorpusFile(
    string FileName,
    InstructionsFileParsedResult Parsed,
    string ContentHash,
    bool HasChangelog);
