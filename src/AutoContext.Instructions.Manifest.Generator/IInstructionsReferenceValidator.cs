namespace AutoContext.Instructions.Manifest.Generator;

using AutoContext.Instructions.Parser;

/// <summary>
/// Resolves every parsed corpus file's <c>[locator#fragment]</c> references against
/// a whole-corpus catalog and aggregates the faults. This is the build-time driver
/// for the pure <see cref="InstructionsFileReferenceResolver"/>:
/// it projects the parsed corpus into a catalog once, then resolves each file's
/// references against it, collecting every finding rather than stopping at the first
/// so a single build report can list them all. Deciding which finding kinds fail the
/// build is the caller's concern.
/// </summary>
internal interface IInstructionsReferenceValidator
{
    /// <summary>
    /// Resolves every reference in <paramref name="corpus"/> against the catalog
    /// the corpus itself forms, aggregating one finding per unresolved reference.
    /// </summary>
    /// <param name="corpus">The parsed corpus to validate.</param>
    /// <returns>Every cross-file reference fault, ordered by source file then by the
    /// reference's body position; empty when every reference resolves.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="corpus"/> is
    /// <see langword="null"/>.</exception>
    IReadOnlyList<InstructionsFileReferenceFindingEntry> Validate(IReadOnlyDictionary<string, InstructionsFileParsedFile> corpus);
}
