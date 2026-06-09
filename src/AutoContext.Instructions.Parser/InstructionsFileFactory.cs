namespace AutoContext.Instructions.Parser;

using AutoContext.Instructions.Parser.Model;

/// <summary>
/// Creates <see cref="InstructionsFile"/> instances from instructions files on
/// disk. It is the disk-backed entry point to the parser: give it a path and it
/// hands back the fully structured file.
/// </summary>
public static class InstructionsFileFactory
{
    private static readonly InstructionsFileSyntaxParser SyntaxParser = new();

    /// <summary>
    /// Creates an <see cref="InstructionsFile"/> from the instructions file at
    /// <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The instructions file to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The structured instructions file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    public static async Task<InstructionsFile> FromFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var tree = await SyntaxParser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);

        return InstructionsFile.FromSpans(tree);
    }
}
