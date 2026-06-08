namespace AutoContext.Instructions.Parser;

/// <summary>
/// The category of an <see cref="InstructionsFileSpanDiagnostic"/> raised while
/// parsing a single instructions file. These describe file-local syntax faults
/// only; cross-file concerns such as a reference whose target does not resolve
/// once the whole corpus is known live in a later validation layer, not here.
/// </summary>
public enum InstructionsFileSpanDiagnosticKind
{
    /// <summary>A rule bullet under the <c>## Rules</c> section carries no
    /// <c>INST####</c> tag and is therefore unfilterable.</summary>
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

    /// <summary>A tagged rule bullet appears outside the addressable-rule region.
    /// Tagged rules are allowed under the <c>## Rules</c> heading and any
    /// <c>###</c> subsection nested within it; this diagnostic fires when a tagged
    /// bullet appears anywhere else, where such bullets are not recognised as
    /// addressable rules.</summary>
    MisplacedRule,
}
