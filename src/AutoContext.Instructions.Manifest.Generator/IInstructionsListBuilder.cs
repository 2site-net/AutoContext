namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Builds the wire-shape <c>instructions-files.json</c> catalogue from an
/// already-parsed curated corpus.
/// </summary>
internal interface IInstructionsListBuilder
{
    /// <summary>
    /// Builds the manifest from every parsed corpus file, ordered by key.
    /// </summary>
    /// <param name="corpus">The parsed corpus, keyed by basename stem.</param>
    /// <returns>The wire-shape manifest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="corpus"/>
    /// is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A corpus file has malformed
    /// or missing frontmatter.</exception>
    InstructionsManifest Build(IReadOnlyDictionary<string, CorpusFileParsedResult> corpus);
}
