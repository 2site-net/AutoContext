namespace AutoContext.Instructions.Parser;

/// <summary>
/// Why a parsed <see cref="InstructionsFileReference"/> failed to resolve against
/// the corpus catalog. Unlike <see cref="InstructionsFileDiagnosticKind"/>,
/// which reports <em>syntactic</em> faults a single file can detect on its own,
/// these are <em>cross-file</em> faults that only surface once every file's rules
/// and sections are known.
/// </summary>
public enum InstructionsFileReferenceFindingKind
{
    /// <summary>The locator names a file the catalog does not contain — a
    /// mistyped key or a removed file (<c>[nosuchfile#INST0001]</c>). URI
    /// locators are existence-unverified and never raise this.</summary>
    UnknownLocator,

    /// <summary>The locator resolves to a known file, but that file defines no
    /// rule with the cited <c>INST####</c> id — a dangling rule reference
    /// (<c>[testing#INST9999]</c>).</summary>
    DanglingRuleReference,

    /// <summary>The locator resolves to a known file, but no section in it
    /// matches the cited heading by anchor or exact text
    /// (<c>[testing#'No Such Section']</c>).</summary>
    UnresolvedSectionReference,

    /// <summary>The reference names its own file's catalog key instead of
    /// omitting the locator. Same-file references must use the bare
    /// <c>[#INST0014]</c> / <c>[#'Assertions']</c> form; spelling the own domain
    /// (<c>[testing#INST0014]</c> inside <c>testing</c>) is redundant.</summary>
    RedundantLocator,
}
