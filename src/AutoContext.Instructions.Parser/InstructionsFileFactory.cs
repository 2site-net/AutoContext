namespace AutoContext.Instructions.Parser;

using AutoContext.Instructions.Parser.Model;

/// <summary>
/// Reads instructions files from disk and turns them into <see cref="InstructionsFile"/>
/// values. It does the scanning for you: it runs a shared
/// <see cref="InstructionsFileSyntaxParser"/> over the file in its default
/// <see cref="Syntax.InstructionsFileSpanEmitLevel.Full"/> /
/// <see cref="Syntax.InstructionsFileSpanEmitScope.All"/> configuration and passes the spans
/// straight to <see cref="InstructionsFile.FromSpans"/>.
/// </summary>
public static class InstructionsFileFactory
{
    private static readonly InstructionsFileSyntaxParser SyntaxParser = new();

    /// <summary>
    /// Reads the instructions file at <paramref name="path"/> and builds its
    /// structured <see cref="InstructionsFile"/>.
    /// </summary>
    /// <param name="path">The instructions file to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parsed instructions file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    public static async Task<InstructionsFile> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var spans = await SyntaxParser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);

        return InstructionsFile.FromSpans(spans);
    }
}
