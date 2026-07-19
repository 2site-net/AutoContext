namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// The structured fault codes <see cref="InstructionsMetadataSearchService"/>
/// returns for an invalid predicate, mirroring the reference TS engine so the
/// stdio and pipe surfaces stay equivalent.
/// </summary>
internal enum InstructionsMetadataSearchErrorKind
{
    /// <summary>A predicate key that names no recognised field.</summary>
    UnknownField,

    /// <summary>
    /// A predicate value whose JSON kind does not match the field's expected
    /// type (e.g. a string for <c>hasChangelog</c>).
    /// </summary>
    TypeMismatch,

    /// <summary>A string predicate value that is not a valid regex.</summary>
    InvalidRegex,

    /// <summary>
    /// A regex predicate value longer than the pattern-length cap.
    /// </summary>
    PatternTooLong,
}
