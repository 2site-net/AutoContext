namespace AutoContext.Instructions.Parser.Model;

using System.Text.RegularExpressions;

using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// The parts of an instructions file's frontmatter that the parser reads. Every
/// field is optional here: a file may have no frontmatter at all, in which case
/// they are all <see langword="null"/>. Checking that the fields are present and
/// well-formed (a required <c>name</c> in <c>&lt;key&gt; (vX.Y.Z)</c> form, a
/// non-empty <c>description</c>) is up to the consumer, not the parser.
/// </summary>
/// <param name="RawValue">The text between the leading <c>---</c> fences exactly as
/// written — not the fences or the newlines around them — or the empty string when
/// the file has no frontmatter.</param>
/// <param name="Name">The <c>name</c> field as written (expected to be
/// <c>&lt;key&gt; (vX.Y.Z)</c>), or <see langword="null"/> when missing.</param>
/// <param name="Description">The <c>description</c> field as written, or
/// <see langword="null"/> when missing.</param>
/// <param name="ApplyTo">The parsed <c>applyTo</c> glob, or <see langword="null"/>
/// when the file has no <c>applyTo</c> (for example, a file that always
/// applies).</param>
/// <param name="Version">The version taken from the <c>(vX.Y.Z)</c> suffix of
/// <paramref name="Name"/>, or <see langword="null"/> when <paramref name="Name"/>
/// has no such suffix.</param>
public sealed partial record InstructionsFileFrontmatter(
    string RawValue,
    string? Name,
    string? Description,
    FrontmatterApplyTo? ApplyTo,
    string? Version)
{
    private enum FrontmatterField
    {
        Unknown,
        Name,
        Description,
        ApplyTo,
    }

    /// <summary>
    /// Builds an <see cref="InstructionsFileFrontmatter"/> from the frontmatter span
    /// stream of a parsed file. The
    /// <see cref="InstructionsFileSpanKind.FrontmatterBlock"/> span supplies the
    /// verbatim <see cref="RawValue"/>, and the
    /// <see cref="InstructionsFileSpanKind.FrontmatterKey"/> /
    /// <see cref="InstructionsFileSpanKind.FrontmatterValue"/> spans supply the
    /// fields. An empty stream yields an empty frontmatter (every field
    /// <see langword="null"/> and <see cref="RawValue"/> empty), which is what a file
    /// with no frontmatter produces.
    /// </summary>
    /// <param name="spans">The frontmatter spans, in document order.</param>
    /// <returns>The parsed frontmatter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spans"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFileFrontmatter FromSpans(IReadOnlyList<InstructionsFileSyntaxSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);

        var rawValue = string.Empty;
        string? name = null;
        string? description = null;
        string? applyToRaw = null;
        var currentField = FrontmatterField.Unknown;

        foreach (var span in spans)
        {
            if (span.Kind == InstructionsFileSpanKind.FrontmatterBlock)
            {
                rawValue = ExtractRawValue(span);
            }
            else if (span.Kind == InstructionsFileSpanKind.FrontmatterKey)
            {
                currentField = ClassifyFrontmatterKey(span.Text.Span);
                AssignFrontmatterField(currentField, string.Empty);
            }
            else if (span.Kind == InstructionsFileSpanKind.FrontmatterValue)
            {
                AssignFrontmatterField(currentField, span.Text.ToString());
            }
        }

        var version = name is null ? null : ExtractVersion(name);
        var applyTo = applyToRaw is null ? null : FrontmatterApplyToParser.Parse(applyToRaw);

        return new InstructionsFileFrontmatter(rawValue, name, description, applyTo, version);

        void AssignFrontmatterField(FrontmatterField field, string value)
        {
            switch (field)
            {
                case FrontmatterField.Name:
                    name = value;
                    break;
                case FrontmatterField.Description:
                    description = value;
                    break;
                case FrontmatterField.ApplyTo:
                    applyToRaw = value;
                    break;
                case FrontmatterField.Unknown:
                default:
                    break;
            }
        }
    }

    private static FrontmatterField ClassifyFrontmatterKey(ReadOnlySpan<char> key)
        => key switch
        {
            "name" => FrontmatterField.Name,
            "description" => FrontmatterField.Description,
            "applyTo" => FrontmatterField.ApplyTo,
            _ => FrontmatterField.Unknown,
        };

    private static string ExtractRawValue(InstructionsFileSyntaxSpan block)
    {
        // The block always starts at the file origin, so the regex runs over the
        // recovered source bounded to the block's length — no substring is copied.
        var source = block.RecoverSourceText();
        var match = GeneratedFrontmatterBlockRegex().Match(source, 0, block.TextSpan.Length);

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string? ExtractVersion(string name)
    {
        var match = GeneratedVersionSuffixRegex().Match(name);

        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^---\r?\n([\s\S]*?)\r?\n---")]
    private static partial Regex GeneratedFrontmatterBlockRegex();

    [GeneratedRegex(@"\(v(\d+\.\d+\.\d+)\)")]
    private static partial Regex GeneratedVersionSuffixRegex();
}
