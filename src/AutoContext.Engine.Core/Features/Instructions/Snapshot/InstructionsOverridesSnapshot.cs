namespace AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Immutable snapshot of the workspace override inventory: the
/// <c>*.instructions.md</c> files present under the configured override
/// directories, keyed by file name (basename) with their absolute
/// on-disk path. Produced by <see cref="InstructionsOverridesWatcher"/>
/// on each settled rescan — already merged across directories with
/// first-directory-wins precedence — and consumed downstream to decide
/// which bundled instruction files a workspace-local copy shadows. A
/// reader holds the reference it observed and is never mutated, so
/// iteration is lock-free and never tears.
/// </summary>
internal sealed class InstructionsOverridesSnapshot
{
    /// <summary>
    /// The shared empty inventory: no override files. This is the value
    /// <see cref="InstructionsOverridesWatcher"/> exposes before its
    /// initial scan completes and whenever the override directory is
    /// absent.
    /// </summary>
    public static InstructionsOverridesSnapshot Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, string> _pathsByFileName;

    /// <summary>
    /// Creates an inventory over <paramref name="pathsByFileName"/>,
    /// mapping each override file name (basename, including the
    /// <c>.instructions.md</c> extension) to its absolute path. The
    /// entries are copied into an owned, case-insensitive ordinal map, so
    /// override matching is robust to filesystem case folding and the
    /// caller may reuse or mutate its dictionary afterwards.
    /// </summary>
    /// <param name="pathsByFileName">Map of override file name to absolute
    /// path. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pathsByFileName"/> is <see langword="null"/>.
    /// </exception>
    public InstructionsOverridesSnapshot(IReadOnlyDictionary<string, string> pathsByFileName)
    {
        ArgumentNullException.ThrowIfNull(pathsByFileName);

        _pathsByFileName = new Dictionary<string, string>(pathsByFileName, StringComparer.OrdinalIgnoreCase);
        FileNames = [.. _pathsByFileName.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The override file names in this inventory, ordinal-sorted for
    /// deterministic iteration.
    /// </summary>
    public IReadOnlyList<string> FileNames { get; }

    /// <summary>
    /// The number of override files in this inventory.
    /// </summary>
    public int Count => _pathsByFileName.Count;

    /// <summary>
    /// Returns whether a workspace override exists for
    /// <paramref name="fileName"/> (case-insensitive ordinal).
    /// </summary>
    /// <param name="fileName">The instruction file name to test,
    /// including the <c>.instructions.md</c> extension. Must not be
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/>
    /// is <see langword="null"/>.</exception>
    public bool Contains(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return _pathsByFileName.ContainsKey(fileName);
    }

    /// <summary>
    /// Looks up the absolute path of the override for
    /// <paramref name="fileName"/> (case-insensitive ordinal).
    /// </summary>
    /// <param name="fileName">The instruction file name to look up,
    /// including the <c>.instructions.md</c> extension. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="path">The absolute override path when one exists;
    /// otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an override exists for
    /// <paramref name="fileName"/>; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/>
    /// is <see langword="null"/>.</exception>
    public bool TryGetPath(string fileName, out string? path)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return _pathsByFileName.TryGetValue(fileName, out path);
    }
}
