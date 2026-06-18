namespace AutoContext.Engine.Core.Infrastructure;

/// <summary>
/// A resources directory with an optional per-file override overlay. A
/// file present under the override root shadows the same-named file under
/// the base root; every file the override root does <em>not</em> carry
/// falls through to the base root. This lets a test or embedder override
/// only the side-cars it cares about (for example a <c>workers.json</c> +
/// <c>mcp-tools-registry.json</c> pair) without copying the entire
/// <c>Resources</c> tree into the override location.
/// </summary>
/// <remarks>
/// The base root is the engine's real <c>Resources</c> directory; the
/// override root is <see cref="EngineOptions.ResourcesRootOverride"/> when
/// set. A plain directory string implicitly widens to an
/// <see cref="EngineResourcesDirectory"/> with no override, so callers
/// that never override — every production loader test among them — keep
/// passing a bare path.
/// </remarks>
internal sealed class EngineResourcesDirectory
{
    private readonly string _baseDirectory;
    private readonly string? _overrideDirectory;

    /// <summary>
    /// Creates a resources directory rooted at
    /// <paramref name="baseDirectory"/>, optionally shadowed per-file by
    /// <paramref name="overrideDirectory"/>.
    /// </summary>
    /// <param name="baseDirectory">Absolute path of the base resources
    /// directory. Must not be <see langword="null"/>, empty, or
    /// whitespace.</param>
    /// <param name="overrideDirectory">Absolute path of the override root
    /// whose files shadow same-named base files, or <see langword="null"/>
    /// for no overlay. When non-null it must not be empty or
    /// whitespace.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseDirectory"/> is <see langword="null"/>, empty,
    /// or whitespace, or <paramref name="overrideDirectory"/> is non-null
    /// but empty or whitespace.</exception>
    public EngineResourcesDirectory(string baseDirectory, string? overrideDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        if (overrideDirectory is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(overrideDirectory);
        }

        _baseDirectory = baseDirectory;
        _overrideDirectory = overrideDirectory;
    }

    /// <summary>
    /// The base resources directory, used for diagnostics. File reads must
    /// go through <see cref="ResolveFile"/> so the overlay is honoured.
    /// </summary>
    public string BaseDirectory
        => _baseDirectory;

    /// <summary>
    /// The per-file override root that shadows <see cref="BaseDirectory"/>,
    /// or <see langword="null"/> when no overlay is active. Diagnostics
    /// only — file reads must go through <see cref="ResolveFile"/> so the
    /// per-file fall-through is honoured.
    /// </summary>
    public string? OverrideDirectory
        => _overrideDirectory;

    /// <summary>
    /// Named alternative to the implicit string conversion: an overlay
    /// rooted at <paramref name="baseDirectory"/> with no override.
    /// </summary>
    /// <param name="baseDirectory">Absolute path of the base resources
    /// directory. Must not be <see langword="null"/>, empty, or
    /// whitespace.</param>
    /// <returns>An overlay with no override layer.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseDirectory"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    public static EngineResourcesDirectory FromDirectory(string baseDirectory)
        => new(baseDirectory);

    /// <summary>
    /// Resolves <paramref name="fileName"/> to the path the caller should
    /// read: the override copy when one exists, otherwise the base copy.
    /// The returned base path is not guaranteed to exist — callers keep
    /// their own missing-file handling.
    /// </summary>
    /// <param name="fileName">File name (or relative path) under the
    /// resources directory. Must not be <see langword="null"/>, empty, or
    /// whitespace.</param>
    /// <returns>The override path when the file exists there; otherwise the
    /// base path.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="fileName"/> is <see langword="null"/>, empty, or
    /// whitespace.</exception>
    public string ResolveFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (_overrideDirectory is not null)
        {
            var candidate = Path.Combine(_overrideDirectory, fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(_baseDirectory, fileName);
    }

    /// <summary>
    /// Returns the same overlay narrowed to the
    /// <paramref name="name"/> subdirectory of both roots, so a consumer
    /// rooted at a subtree (for example <c>Resources/Instructions</c>)
    /// keeps the override fall-through for its own files.
    /// </summary>
    /// <param name="name">Subdirectory name. Must not be
    /// <see langword="null"/>, empty, or whitespace.</param>
    /// <returns>An overlay rooted at the subdirectory of both roots.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is <see langword="null"/>, empty, or
    /// whitespace.</exception>
    public EngineResourcesDirectory SubDirectory(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new EngineResourcesDirectory(
            Path.Combine(_baseDirectory, name),
            _overrideDirectory is null ? null : Path.Combine(_overrideDirectory, name));
    }

    /// <summary>
    /// Widens a plain base directory path to an overlay with no override.
    /// </summary>
    /// <param name="baseDirectory">Absolute path of the base resources
    /// directory.</param>
    public static implicit operator EngineResourcesDirectory(string baseDirectory)
    {
        return FromDirectory(baseDirectory);
    }
}
