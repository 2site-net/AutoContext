namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The gateway for reading an instructions file off disk and parsing it. Mirrors
/// <see cref="File"/>: a static surface named for the noun, whose verb
/// methods take a path. Every method reads the file and funnels its content into
/// <see cref="InstructionsFileParser.Parse"/>, which performs the actual structural
/// parse and never touches the file system itself.
/// </summary>
public static class InstructionsFile
{
    /// <summary>
    /// Reads and parses the instructions file at <paramref name="path"/>, throwing
    /// when the file cannot be read.
    /// </summary>
    /// <param name="path">The instructions file to read: a bare file name, a path
    /// relative to the current directory, or an absolute path.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file does not exist, or it is locked or
    /// otherwise inaccessible.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller lacks permission to
    /// read the file.</exception>
    public static InstructionsFileParsedResult Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return InstructionsFileParser.Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Asynchronously reads and parses the instructions file at
    /// <paramref name="path"/>, throwing when the file cannot be read.
    /// </summary>
    /// <param name="path">The instructions file to read: a bare file name, a path
    /// relative to the current directory, or an absolute path.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file does not exist, or it is locked or
    /// otherwise inaccessible.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller lacks permission to
    /// read the file.</exception>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public static async Task<InstructionsFileParsedResult> ParseAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return InstructionsFileParser.Parse(content);
    }

    /// <summary>
    /// Reads and parses the instructions file at <paramref name="path"/>, returning
    /// <see langword="false"/> instead of throwing when the file cannot be read — it
    /// does not exist, or it is locked or otherwise inaccessible.
    /// </summary>
    /// <param name="path">The instructions file to read: a bare file name, a path
    /// relative to the current directory, or an absolute path.</param>
    /// <param name="result">The complete structural parse when the method returns
    /// <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the file was read and parsed;
    /// <see langword="false"/> when it could not be read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    public static bool TryParse(string path, [NotNullWhen(true)] out InstructionsFileParsedResult? result)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            result = Parse(path);
            return true;
        }
        catch (IOException)
        {
            result = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Asynchronously reads and parses the instructions file at
    /// <paramref name="path"/>, reporting failure through the returned result
    /// instead of throwing when the file cannot be read — it does not exist, or it
    /// is locked or otherwise inaccessible.
    /// </summary>
    /// <param name="path">The instructions file to read: a bare file name, a path
    /// relative to the current directory, or an absolute path.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>A successful result carrying the parse, or a failed result carrying
    /// the reason the read could not complete.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public static async Task<InstructionsFileTryResult> TryParseAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            var parsed = await ParseAsync(path, cancellationToken).ConfigureAwait(false);
            return InstructionsFileTryResult.Ok(parsed);
        }
        catch (IOException exception)
        {
            return InstructionsFileTryResult.Fail(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return InstructionsFileTryResult.Fail(exception.Message);
        }
    }
}
