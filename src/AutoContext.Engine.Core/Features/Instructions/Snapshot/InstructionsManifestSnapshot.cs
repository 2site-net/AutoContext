namespace AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Immutable snapshot of the bundled instructions corpus: the category
/// taxonomy the catalog declares, plus the merged
/// <see cref="InstructionsFileManifestEntry"/> rows the engine ships, in
/// document order, with an ordinal lookup by file name. Built once at
/// startup by <see cref="InstructionsManifestLoader"/> from the
/// build-time catalog and manifest side-cars and published through
/// <see cref="IInstructionsManifestAccessor"/>. A reader holds the
/// reference it observed and is never mutated, so iteration is lock-free
/// and never tears.
/// </summary>
internal sealed class InstructionsManifestSnapshot
{
    /// <summary>
    /// The shared empty snapshot: no categories, no files. This is the
    /// value <see cref="InstructionsManifestService"/> exposes before its
    /// startup load completes.
    /// </summary>
    public static InstructionsManifestSnapshot Empty { get; } = new([], []);

    private readonly Dictionary<string, InstructionsFileManifestEntry> _byFileName;

    /// <summary>
    /// Creates a snapshot over <paramref name="categories"/> and
    /// <paramref name="files"/>, building the ordinal file-name lookup.
    /// </summary>
    /// <param name="categories">The category taxonomy, in document
    /// order. Must not be <see langword="null"/>.</param>
    /// <param name="files">The merged corpus rows, in document order.
    /// Must not be <see langword="null"/>, contain a
    /// <see langword="null"/> element, or contain two rows with the same
    /// <see cref="InstructionsFileManifestEntry.FileName"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="categories"/> or <paramref name="files"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="files"/>
    /// contains a <see langword="null"/> element or a duplicate file
    /// name.</exception>
    public InstructionsManifestSnapshot(
        IReadOnlyList<InstructionsCategoryEntry> categories,
        IReadOnlyList<InstructionsFileManifestEntry> files)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(files);

        _byFileName = new Dictionary<string, InstructionsFileManifestEntry>(
            files.Count, StringComparer.Ordinal);

        foreach (var file in files)
        {
            ArgumentNullException.ThrowIfNull(file);

            if (!_byFileName.TryAdd(file.FileName, file))
            {
                throw new ArgumentException(
                    $"Duplicate instruction file name '{file.FileName}' in corpus.",
                    nameof(files));
            }
        }

        Categories = categories;
        Files = files;
    }

    /// <summary>
    /// The category taxonomy in document order.
    /// </summary>
    public IReadOnlyList<InstructionsCategoryEntry> Categories { get; }

    /// <summary>
    /// The merged corpus rows in document order.
    /// </summary>
    public IReadOnlyList<InstructionsFileManifestEntry> Files { get; }

    /// <summary>
    /// Returns the row whose
    /// <see cref="InstructionsFileManifestEntry.FileName"/> equals
    /// <paramref name="fileName"/> (ordinal), or <see langword="null"/>
    /// when no row matches.
    /// </summary>
    /// <param name="fileName">The corpus file name to look up, including
    /// the <c>.instructions.md</c> extension. Must not be
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/>
    /// is <see langword="null"/>.</exception>
    public InstructionsFileManifestEntry? FindByFileName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return _byFileName.GetValueOrDefault(fileName);
    }
}
