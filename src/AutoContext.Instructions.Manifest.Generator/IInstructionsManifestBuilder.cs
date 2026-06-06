namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Builds the build-generated <c>instructions-manifest.json</c> fact index from an
/// already-parsed curated corpus. It folds the former wire-list and metadata
/// builders into a single pass: it validates each file's curatorial frontmatter
/// shape (name, key, description, <c>applyTo</c>) and extracts its derived facts
/// (section map, <c>applyTo</c> extension set, content hash, changelog flag).
/// </summary>
internal interface IInstructionsManifestBuilder
{
    /// <summary>
    /// Builds the manifest from every parsed corpus file, ordered by key.
    /// </summary>
    /// <param name="corpus">The parsed corpus, keyed by basename stem.</param>
    /// <returns>The generated fact manifest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="corpus"/>
    /// is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A corpus file has malformed or
    /// missing frontmatter, or a duplicate section anchor.</exception>
    JsonInstructionsManifest Build(IReadOnlyDictionary<string, CorpusFileParsedResult> corpus);
}
