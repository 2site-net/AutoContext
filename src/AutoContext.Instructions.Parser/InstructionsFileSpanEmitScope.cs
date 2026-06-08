namespace AutoContext.Instructions.Parser;

/// <summary>
/// Selects which logical groups of spans an <see cref="InstructionsFileSpanParser"/>
/// emits. An additive union of kind groups — selecting a group adds its spans to
/// the stream. Combines with <see cref="InstructionsFileSpanEmitLevel"/> by
/// intersection: a span is emitted only when its kind belongs to a selected level
/// <em>and</em> a selected scope.
/// </summary>
[Flags]
public enum InstructionsFileSpanEmitScope
{
    /// <summary>Emit nothing.</summary>
    None = 0,

    /// <summary>Emit ordinary <see cref="InstructionsFileSpanKind.Text"/> spans.</summary>
    Text = 1 << 0,

    /// <summary>Emit frontmatter spans — the block and its property, key, and
    /// value tokens.</summary>
    Frontmatter = 1 << 1,

    /// <summary>Emit heading spans of every level.</summary>
    Headings = 1 << 2,

    /// <summary>Emit rule spans — plain and tagged rules and their tag tokens.</summary>
    Rules = 1 << 3,

    /// <summary>Emit reference token spans.</summary>
    References = 1 << 4,

    /// <summary>Broad shortcut for every non-frontmatter group:
    /// <see cref="Text"/> | <see cref="Headings"/> | <see cref="Rules"/> |
    /// <see cref="References"/>. Because it already subsumes the narrower body
    /// groups, <c>Body | Headings</c> is equivalent to <c>Body</c>.</summary>
    Body = Text | Headings | Rules | References,

    /// <summary>Emit spans from the whole file: <see cref="Frontmatter"/> |
    /// <see cref="Body"/>.</summary>
    All = Frontmatter | Body,
}
