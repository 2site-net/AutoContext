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
/// The base root is whichever side-car directory the caller asked for —
/// <c>Resources/</c> for the manifests, <c>Instructions/</c> for the curated
/// corpus; the override root is
/// <see cref="EngineOptions.ResourcesRootOverride"/> when
/// set. A plain directory string implicitly widens to an
/// <see cref="EngineResourcesDirectory"/> with no override, so callers
/// that never override — every production loader test among them — keep
/// passing a bare path.
/// </remarks>
internal sealed class EngineResourcesDirectory
{
    private const string InstructionsDirectoryName = "Instructions";
    private const string ResourcesDirectoryName = "Resources";

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
    /// The curated instruction corpus — <c>Instructions/</c> at the bundle
    /// root beside <c>Resources/</c> and <c>Workers/</c>, because the corpus
    /// is authored content rather than a generated manifest. The override
    /// root mirrors the same bundle shape, so it narrows by the same segment.
    /// </summary>
    /// <param name="options">The engine options carrying the optional
    /// override root. Must not be <see langword="null"/>.</param>
    /// <returns>The corpus overlay.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.</exception>
    public static EngineResourcesDirectory ForInstructions(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new EngineResourcesDirectory(AppContext.BaseDirectory, options.ResourcesRootOverride)
            .SubDirectory(InstructionsDirectoryName);
    }

    /// <summary>
    /// The generated and hand-authored manifest side-cars — <c>Resources/</c>
    /// beside the engine binary, shadowed per-file by
    /// <see cref="EngineOptions.ResourcesRootOverride"/> when it is set.
    /// </summary>
    /// <param name="options">The engine options carrying the optional
    /// override root. Must not be <see langword="null"/>.</param>
    /// <returns>The manifest side-car overlay.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.</exception>
    public static EngineResourcesDirectory ForResources(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new EngineResourcesDirectory(
            Path.Combine(AppContext.BaseDirectory, ResourcesDirectoryName),
            options.ResourcesRootOverride);
    }

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
    /// rooted at a subtree (for example the curated corpus under
    /// <c>Instructions</c>)
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
