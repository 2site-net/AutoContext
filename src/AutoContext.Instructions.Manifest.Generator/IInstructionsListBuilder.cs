namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Builds the wire-shape <c>instructions-files.json</c> catalogue from a
/// curated instruction corpus directory.
/// </summary>
internal interface IInstructionsListBuilder
{
    /// <summary>
    /// Builds the manifest from every <c>*.instructions.md</c> file in
    /// <paramref name="corpusDirectory"/>, ordered by key.
    /// </summary>
    /// <param name="corpusDirectory">The curated corpus directory.</param>
    /// <returns>The wire-shape manifest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="corpusDirectory"/>
    /// is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A corpus file has malformed
    /// or missing frontmatter.</exception>
    InstructionsManifest Build(string corpusDirectory);
}
