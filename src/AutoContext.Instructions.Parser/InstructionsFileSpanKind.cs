namespace AutoContext.Instructions.Parser;

/// <summary>
/// The syntax role of an <see cref="InstructionsFileParsedSpan"/>. A kind names
/// what a span <em>is</em> as written — never whether it is well-formed. A
/// malformed construct keeps its natural kind (a bad rule tag is still a
/// <see cref="Tag"/>); the fault is carried by an attached
/// <see cref="InstructionsFileDiagnostic"/>, not by a diagnostic-specific
/// kind.
/// </summary>
public enum InstructionsFileSpanKind
{
    /// <summary>Plain text that matches no more specific syntax — ordinary body
    /// prose, blank lines, and the interstitial newlines between recognised
    /// block spans. <see cref="InstructionsFileSpanEmitLevel.Blocks"/> emits these
    /// to keep its partition gapless.</summary>
    Text,

    /// <summary>The leading frontmatter block, opening and closing <c>---</c>
    /// delimiters included, together with the closing delimiter's trailing line
    /// terminator. A block-level kind.</summary>
    FrontmatterBlock,

    /// <summary>One <c>key: value</c> property inside the frontmatter block. A
    /// token-level kind that contains a <see cref="FrontmatterKey"/> and a
    /// <see cref="FrontmatterValue"/>.</summary>
    FrontmatterProperty,

    /// <summary>The key half of a frontmatter property — the <c>key</c> in
    /// <c>key: value</c>. A token-level kind.</summary>
    FrontmatterKey,

    /// <summary>The value half of a frontmatter property — the <c>value</c> in
    /// <c>key: value</c>. A token-level kind.</summary>
    FrontmatterValue,

    /// <summary>A level-one heading (<c>#&#160;Heading</c>). A block-level kind.
    /// Heading levels are emitted as written; deciding which headings become
    /// structural sections is the materializer's job.</summary>
    Heading1,

    /// <summary>A level-two heading (<c>##&#160;Section</c>). A block-level
    /// kind.</summary>
    Heading2,

    /// <summary>A level-three heading (<c>###&#160;Subsection</c>). A block-level
    /// kind.</summary>
    Heading3,

    /// <summary>A rule bullet that carries no instruction tag
    /// (<c>-&#160;**Do**&#160;…</c>). A block-level kind.</summary>
    PlainRule,

    /// <summary>A rule bullet that carries an instruction tag
    /// (<c>-&#160;[INST0001]&#160;**Do**&#160;…</c>). A block-level kind that
    /// contains a <see cref="Tag"/> and any <see cref="Reference"/> tokens.</summary>
    TaggedRule,

    /// <summary>The exact bracketed tag inside a tagged rule (<c>[INST0001]</c>).
    /// A token-level kind.</summary>
    Tag,

    /// <summary>The exact cross-reference token (<c>[locator#fragment]</c>),
    /// excluding the surrounding prose. A token-level kind.</summary>
    Reference,
}
