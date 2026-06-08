namespace AutoContext.Instructions.Parser;

/// <summary>
/// Selects the detail layers an <see cref="InstructionsFileSpanParser"/> emits.
/// Combines with <see cref="InstructionsFileSpanEmitScope"/> by intersection: a
/// span is emitted only when its kind belongs to a selected level <em>and</em> a
/// selected scope.
/// </summary>
[Flags]
public enum InstructionsFileSpanEmitLevel
{
    /// <summary>Emit nothing.</summary>
    None = 0,

    /// <summary>Emit the block layer only — <see cref="InstructionsFileSpanKind.Text"/>,
    /// <see cref="InstructionsFileSpanKind.FrontmatterBlock"/>,
    /// <see cref="InstructionsFileSpanKind.Heading1"/>/<see cref="InstructionsFileSpanKind.Heading2"/>/<see cref="InstructionsFileSpanKind.Heading3"/>,
    /// <see cref="InstructionsFileSpanKind.PlainRule"/>, and
    /// <see cref="InstructionsFileSpanKind.TaggedRule"/>. The block layer is a
    /// gapless, non-overlapping partition of the decoded file text.</summary>
    Blocks = 1 << 0,

    /// <summary>Emit the token layer only — <see cref="InstructionsFileSpanKind.FrontmatterProperty"/>,
    /// <see cref="InstructionsFileSpanKind.FrontmatterKey"/>,
    /// <see cref="InstructionsFileSpanKind.FrontmatterValue"/>,
    /// <see cref="InstructionsFileSpanKind.Tag"/>, and
    /// <see cref="InstructionsFileSpanKind.Reference"/>. The token layer is sparse:
    /// only recognised tokens are emitted, and tokens may nest inside other
    /// tokens.</summary>
    Tokens = 1 << 1,

    /// <summary>Emit both layers. The gapless block partition is preserved while
    /// token spans overlap the block spans that contain them.</summary>
    Full = Blocks | Tokens,
}
