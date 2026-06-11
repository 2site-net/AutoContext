namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// What kind of problem an <see cref="InstructionsFileDiagnostic"/> reports. These
/// cover problems within a single file only; cross-file problems — such as a
/// reference whose target does not exist once every file is known — are checked
/// later, not here.
/// </summary>
public enum InstructionsFileDiagnosticKind
{
    /// <summary>A rule bullet under the <c>## Rules</c> section has no
    /// <c>INST####</c> tag, so it cannot be picked out on its own.</summary>
    MissingTag,

    /// <summary>An <c>INST####</c> tag appears on more than one rule bullet within
    /// the file.</summary>
    DuplicateTag,

    /// <summary>A rule bullet carries a bracket tag that is not a well-formed
    /// <c>INST####</c> identifier.</summary>
    MalformedTag,

    /// <summary>A <c>[locator#fragment]</c> reference token whose locator or
    /// fragment is malformed — a bad locator, a truncated id, or a rule
    /// range.</summary>
    MalformedReference,

    /// <summary>A tagged rule bullet appears where tagged rules are not allowed.
    /// Tagged rules belong under the <c>## Rules</c> heading and any <c>###</c>
    /// subsection within it; this fires when a tagged bullet shows up anywhere
    /// else, where it is not treated as a real rule.</summary>
    MisplacedRule,
}
