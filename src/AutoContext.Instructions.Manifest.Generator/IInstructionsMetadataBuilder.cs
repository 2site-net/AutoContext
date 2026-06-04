namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Enriches a wire-shape <see cref="InstructionsManifest"/> into the catalogue-only
/// <see cref="InstructionsMetadata"/> index by parsing each corpus file's section
/// headings and the extension set derived from its <c>applyTo</c> glob. Deriving
/// from the already-validated manifest keeps the two catalogues describing an
/// identical file set without re-running curatorial validation.
/// </summary>
internal interface IInstructionsMetadataBuilder
{
    /// <summary>
    /// Builds the metadata catalogue from <paramref name="manifest"/>, reading each
    /// entry's source file under <paramref name="corpusDirectory"/> to extract its
    /// section index and parsed <c>applyTo</c> extension set.
    /// </summary>
    /// <param name="manifest">The validated wire-shape manifest to enrich.</param>
    /// <param name="corpusDirectory">The curated corpus directory the manifest was
    /// built from.</param>
    /// <returns>The section- and extension-indexed metadata catalogue.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> or
    /// <paramref name="corpusDirectory"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Two sections in one file slug to
    /// the same anchor.</exception>
    InstructionsMetadata Build(InstructionsManifest manifest, string corpusDirectory);
}
