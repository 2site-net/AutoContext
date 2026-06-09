namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// What an <see cref="Model.InstructionsFileReference"/> points at. The fragment after
/// the <c>#</c> decides: an <c>INST####</c> id on its own is a <see cref="Rule"/>
/// reference, a single-quoted heading is a <see cref="Section"/> reference.
/// </summary>
public enum InstructionsFileReferenceKind
{
    /// <summary>A reference to one rule by its <c>INST####</c> id
    /// (<c>[testing#INST0014]</c>, or same-file <c>[#INST0014]</c>).</summary>
    Rule,

    /// <summary>A reference to a section by its single-quoted heading
    /// (<c>[testing#'Test Support']</c>, or same-file <c>[#'Assertions']</c>). A
    /// literal apostrophe in the heading is backslash-escaped
    /// (<c>[#'Bob\'s rules']</c>).</summary>
    Section,
}
