namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// One excerpt of an <see cref="InstructionsSearchBodyHit"/>: a trimmed window
/// of projected body text around a match, with the anchor of the section it
/// falls in (for chaining into <c>Instructions.Get</c> section slicing) and
/// the one-based body line the match starts on.
/// </summary>
/// <param name="Anchor">The anchor of the section the match falls in, or an
/// empty string when the match precedes the first heading.</param>
/// <param name="Snippet">The trimmed excerpt text around the match.</param>
/// <param name="Line">The one-based body line the match starts on, or
/// <see langword="null"/> when not tracked.</param>
internal sealed record InstructionsSearchBodyExcerpt(
    string Anchor,
    string Snippet,
    int? Line);
