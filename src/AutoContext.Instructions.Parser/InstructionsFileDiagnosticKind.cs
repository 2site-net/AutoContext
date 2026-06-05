namespace AutoContext.Instructions.Parser;

/// <summary>
/// The category of an <see cref="InstructionsFileDiagnostic"/> raised while
/// parsing an instruction file's rule bullets and references. Consumers decide
/// which kinds are fatal.
/// </summary>
public enum InstructionsFileDiagnosticKind
{
    /// <summary>An <c>INST####</c> tag appears on more than one bullet.</summary>
    DuplicateId,

    /// <summary>A <c>**Do**</c>/<c>**Don't**</c> bullet carries no
    /// <c>INST####</c> tag and is therefore unfilterable.</summary>
    MissingId,

    /// <summary>A bullet carries a bracket tag that is not a well-formed
    /// <c>INST####</c> identifier.</summary>
    MalformedId,

    /// <summary>A bare <c>[locator#fragment]</c> reference token whose fragment is
    /// neither a well-formed <c>INST####</c> rule id nor a single-quoted section
    /// heading — for example a bad locator, a truncated id, or a rule range.</summary>
    MalformedReference,
}
