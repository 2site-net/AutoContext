namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// Reads the verbatim on-disk body of an instructions file, either from the
/// bundled corpus copy or from a workspace override, without any parsing or
/// projection. The caller names the source; the two are read independently
/// and never fall back to one another.
/// </summary>
internal sealed class InstructionsFileReader
{
    private readonly string _instructionsDirectory;
    private readonly IInstructionsOverridesAccessor _overridesAccessor;

    /// <summary>
    /// Creates a reader that resolves bundled bodies under
    /// <paramref name="instructionsDirectory"/> and override bodies through
    /// <paramref name="overridesAccessor"/>.
    /// </summary>
    /// <param name="instructionsDirectory">Absolute path of the directory
    /// holding the bundled <c>*.instructions.md</c> bodies. Must not be
    /// <see langword="null"/>, empty, or whitespace.</param>
    /// <param name="overridesAccessor">Read seam over the workspace override
    /// inventory, used to resolve an override file's path.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="instructionsDirectory"/> is <see langword="null"/>,
    /// empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overridesAccessor"/> is
    /// <see langword="null"/>.</exception>
    public InstructionsFileReader(
        string instructionsDirectory,
        IInstructionsOverridesAccessor overridesAccessor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructionsDirectory);
        ArgumentNullException.ThrowIfNull(overridesAccessor);

        _instructionsDirectory = instructionsDirectory;
        _overridesAccessor = overridesAccessor;
    }

    /// <summary>
    /// Reads the verbatim bundled body for <paramref name="fileName"/>,
    /// ignoring any workspace override that shadows it.
    /// </summary>
    /// <param name="fileName">The corpus file name, e.g.
    /// <c>testing.instructions.md</c>.</param>
    /// <param name="cancellationToken">A token to observe while
    /// reading.</param>
    /// <returns>The raw bundled content, or <see langword="null"/> when no
    /// bundled file exists on disk.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileName"/> is <see langword="null"/>.</exception>
    public Task<string?> ReadOriginalFileAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return ReadFileIfExistsAsync(
            Path.Combine(_instructionsDirectory, fileName),
            cancellationToken);
    }

    /// <summary>
    /// Reads the verbatim override body for <paramref name="fileName"/>.
    /// </summary>
    /// <param name="fileName">The corpus file name, e.g.
    /// <c>testing.instructions.md</c>.</param>
    /// <param name="cancellationToken">A token to observe while
    /// reading.</param>
    /// <returns>The raw override content, or <see langword="null"/> when no
    /// override is registered for the file or the override file is missing
    /// on disk.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileName"/> is <see langword="null"/>.</exception>
    public Task<string?> ReadOverrideFileAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return _overridesAccessor.Current.TryGetPath(fileName, out var overrideFilePath)
            && overrideFilePath is not null
                ? ReadFileIfExistsAsync(overrideFilePath, cancellationToken)
                : Task.FromResult<string?>(null);
    }

    private static async Task<string?> ReadFileIfExistsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
