namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Reads a curated instructions corpus directory and parses every
/// <c>*.instructions.md</c> file exactly once into a key-addressed corpus the
/// downstream catalog, manifest, and reference-validation stages all share.
/// </summary>
internal interface ICorpusParser
{
    /// <summary>
    /// Reads and parses every <c>*.instructions.md</c> file in
    /// <paramref name="corpusDirectory"/>, keyed by basename stem
    /// (<c>testing.instructions.md</c> &#8594; <c>testing</c>) and inserted in
    /// ordinal filename order for deterministic downstream output.
    /// </summary>
    /// <param name="corpusDirectory">The curated corpus directory.</param>
    /// <param name="cancellationToken">Cancels the corpus read.</param>
    /// <returns>The parsed corpus, keyed by basename stem.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="corpusDirectory"/>
    /// is <see langword="null"/>.</exception>
    Task<IReadOnlyDictionary<string, InstructionsFileParsedFile>> ParseAsync(
        string corpusDirectory,
        CancellationToken cancellationToken = default);
}
