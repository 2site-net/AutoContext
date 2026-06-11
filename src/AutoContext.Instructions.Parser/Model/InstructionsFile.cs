namespace AutoContext.Instructions.Parser.Model;

using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// A single instructions file in memory: its verbatim <see cref="RawContent"/>
/// together with the parsed <see cref="Frontmatter"/>, <see cref="Body"/> (the body
/// text plus its sections and rule bullets), and the file-level
/// <see cref="References"/> and <see cref="Diagnostics"/> gathered from the body
/// prose. Everything that reads instructions files — the build-time manifest
/// generator and the runtime engine — works from this one shape, so each file is
/// parsed just once.
/// <para>
/// Construct one from a span stream with <see cref="FromSyntaxTree"/>, or read and parse
/// a file from disk with <see cref="InstructionsFileFactory.FromFileAsync"/>.
/// </para>
/// </summary>
/// <param name="RawContent">The exact file content, frontmatter and body
/// included.</param>
/// <param name="Frontmatter">The parsed frontmatter from the top of the file.</param>
/// <param name="Body">The parsed body: the body text plus its sections and
/// rules.</param>
/// <param name="References">The <c>[locator#fragment]</c> rule and section
/// references found in the body prose (e.g. <c>[testing#INST0014]</c> or
/// <c>[#'Assertions']</c>), in the order they appear. References inside fenced code
/// blocks and inline code are left out, since those are examples rather than real
/// links. Their offsets are measured from the start of <see cref="Body"/>. See
/// <see cref="InstructionsFileReference"/> for the exact form and what
/// <em>locator</em> and <em>fragment</em> mean.</param>
/// <param name="Diagnostics">Problems found with rule tags (malformed, missing, or
/// duplicate) and with references, in the order they appear. Their line numbers are
/// measured from the start of <see cref="Body"/>.</param>
public sealed record InstructionsFile(
    string RawContent,
    InstructionsFileFrontmatter Frontmatter,
    InstructionsFileBody Body,
    IReadOnlyList<InstructionsFileReference> References,
    IReadOnlyList<InstructionsFileDiagnostic> Diagnostics)
{
    /// <summary>
    /// Builds an <see cref="InstructionsFile"/> from a parsed
    /// <see cref="InstructionsFileSyntaxTree"/>. The tree must be the complete
    /// <see cref="InstructionsFileSpanEmitLevel.Full"/> /
    /// <see cref="InstructionsFileSpanEmitScope.All"/> output for a single file. This
    /// method composes the pieces: it recovers the verbatim <see cref="RawContent"/>
    /// from the spans, hands the frontmatter stream to
    /// <see cref="InstructionsFileFrontmatter.FromSpans"/> and the body stream to
    /// <see cref="InstructionsFileBody.FromSpans"/>, then rebases the reference and
    /// diagnostic side streams from whole-file offsets to body-relative ones using
    /// the body origin recovered from the first body span.
    /// </summary>
    /// <param name="tree">The parsed syntax tree.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFile FromSyntaxTree(InstructionsFileSyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var content = RecoverContent(tree);
        var frontmatter = InstructionsFileFrontmatter.FromSpans(tree.Frontmatter);
        var body = InstructionsFileBody.FromSpans(tree.Body);
        var (charOrigin, lineOrigin) = BodyOrigin(tree.Body);
        var references = BuildReferences(tree.References, charOrigin, lineOrigin);
        var diagnostics = BuildDiagnostics(tree.Diagnostics, lineOrigin);

        return new InstructionsFile(content, frontmatter, body, references, diagnostics);
    }

    private static (int CharOrigin, int LineOrigin) BodyOrigin(IReadOnlyList<InstructionsFileSyntaxSpan> bodySpans)
    {
        if (bodySpans.Count == 0)
        {
            return (0, 0);
        }

        // The first body span is a block at the body origin, so the difference
        // between its absolute and body-relative offsets recovers that origin.
        // Every body span carries an offset.
        var first = bodySpans[0];
        var offset = first.Offset
            ?? throw new InvalidOperationException("A body span must carry an offset, but one was null.");

        return (
            first.TextSpan.StartIndex - offset.StartIndex,
            first.LineSpan.StartLine - offset.StartLine);
    }

    private static List<InstructionsFileDiagnostic> BuildDiagnostics(
        IReadOnlyList<InstructionsFileSyntaxDiagnostic> diagnostics,
        int lineOrigin)
    {
        var list = new List<InstructionsFileDiagnostic>(diagnostics.Count);

        foreach (var diagnostic in diagnostics)
        {
            list.Add(diagnostic.Diagnostic with
            {
                Line = diagnostic.LineSpan.StartLine - lineOrigin,
            });
        }

        return list;
    }

    private static List<InstructionsFileReference> BuildReferences(
        IReadOnlyList<InstructionsFileSyntaxReference> references,
        int charOrigin,
        int lineOrigin)
    {
        var list = new List<InstructionsFileReference>(references.Count);

        foreach (var reference in references)
        {
            list.Add(new InstructionsFileReference(
                reference.Address,
                new InstructionsFileTextSpan(
                    reference.TextSpan.StartIndex - charOrigin,
                    reference.TextSpan.Length),
                reference.LineSpan.StartLine - lineOrigin));
        }

        return list;
    }

    private static string RecoverContent(InstructionsFileSyntaxTree tree)
    {
        // Every span is a window over the single source string the parser read, so the
        // whole file is recovered from any one of them rather than rebuilt by
        // concatenation. An empty tree (an empty file) recovers the empty string.
        if (tree.Frontmatter.Count > 0)
        {
            return tree.Frontmatter[0].RecoverSourceText();
        }

        if (tree.Body.Count > 0)
        {
            return tree.Body[0].RecoverSourceText();
        }

        return string.Empty;
    }
}
