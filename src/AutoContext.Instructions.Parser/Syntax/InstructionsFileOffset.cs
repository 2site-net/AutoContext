namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// The start of a span measured from the start of its own region rather than the
/// start of the file. Only body spans carry one: a body offset counts from the
/// start of the body, as if the leading frontmatter were not there, so a body
/// consumer never has to know how long the frontmatter was. Frontmatter spans have
/// no offset (the region is the file itself, so their region-relative position is
/// just their whole-file position).
/// </summary>
/// <param name="StartIndex">The zero-based character index of the span's start,
/// counted from the start of its region.</param>
/// <param name="StartLine">The zero-based line of the span's start, counted from
/// the start of its region.</param>
public readonly record struct InstructionsFileOffset(int StartIndex, int StartLine);
