namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Reads the hand-authored <c>instructions-catalog.json</c> and cross-validates it
/// against the parsed corpus: every cataloged file exists, every
/// non-always-attached corpus file is cataloged, and every category membership
/// resolves to a declared category. Any discrepancy is a build-fatal authoring
/// error.
/// </summary>
internal interface IInstructionsCatalogReader
{
    /// <summary>
    /// Reads and validates the catalog at <paramref name="catalogPath"/> against
    /// <paramref name="corpus"/>.
    /// </summary>
    /// <param name="catalogPath">The <c>instructions-catalog.json</c> path.</param>
    /// <param name="corpus">The parsed corpus, keyed by basename stem.</param>
    /// <returns>The validated catalog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalogPath"/> or
    /// <paramref name="corpus"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The catalog is malformed, or it
    /// disagrees with the corpus (orphaned entry, uncataloged corpus file, or
    /// undeclared category membership).</exception>
    JsonInstructionsCatalog Read(
        string catalogPath,
        IReadOnlyDictionary<string, InstructionsFileParsedFile> corpus);
}
