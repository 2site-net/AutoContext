namespace AutoContext.Instructions.Parser;

/// <summary>
/// Chooses how much detail an <see cref="InstructionsFileSyntaxParser"/> emits.
/// Works together with <see cref="InstructionsFileSpanEmitScope"/>: a span is
/// emitted only when both its level (here) and its scope are switched on.
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
    /// <see cref="InstructionsFileSpanKind.TaggedRule"/>. The blocks cover the whole
    /// file end to end with no gaps and no overlap.</summary>
    Blocks = 1 << 0,

    /// <summary>Emit the token layer only — <see cref="InstructionsFileSpanKind.FrontmatterProperty"/>,
    /// <see cref="InstructionsFileSpanKind.FrontmatterKey"/>,
    /// <see cref="InstructionsFileSpanKind.FrontmatterValue"/>,
    /// <see cref="InstructionsFileSpanKind.Tag"/>, and
    /// <see cref="InstructionsFileSpanKind.Reference"/>. Only recognised tokens
    /// are emitted, and a token may sit inside another token.</summary>
    Tokens = 1 << 1,

    /// <summary>Emit both. The blocks still cover the whole file, and token spans
    /// overlap the blocks they sit inside.</summary>
    Full = Blocks | Tokens,
}
