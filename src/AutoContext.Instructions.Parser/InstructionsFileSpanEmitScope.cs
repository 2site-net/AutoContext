namespace AutoContext.Instructions.Parser;

/// <summary>
/// Chooses which groups of spans an <see cref="InstructionsFileSpanParser"/>
/// emits. Pick the groups you want and their spans are added. Works together with
/// <see cref="InstructionsFileSpanEmitLevel"/>: a span is emitted only when both
/// its scope (here) and its level are switched on.
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

    /// <summary>Shortcut for every group except frontmatter:
    /// <see cref="Text"/> | <see cref="Headings"/> | <see cref="Rules"/> |
    /// <see cref="References"/>. It already includes those groups, so
    /// <c>Body | Headings</c> is the same as <c>Body</c>.</summary>
    Body = Text | Headings | Rules | References,

    /// <summary>Emit spans from the whole file: <see cref="Frontmatter"/> |
    /// <see cref="Body"/>.</summary>
    All = Frontmatter | Body,
}
