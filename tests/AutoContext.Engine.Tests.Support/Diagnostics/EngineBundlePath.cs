namespace AutoContext.Engine.Tests.Support.Diagnostics;

/// <summary>
/// Resolves paths inside the staged <c>engine/</c> bundle — the flat,
/// self-contained layout a shipped artefact carries, in which the engine
/// binary sits beside the <c>Instructions/</c> corpus, the
/// <c>Resources/</c> manifests, and one <c>Workers/&lt;id&gt;/</c>
/// directory per worker.
/// </summary>
/// <remarks>
/// The bundle is produced by packaging rather than by a project build, so
/// it exists only after <c>scripts/package.ps1</c> has staged it; the smoke
/// pipeline does that via <c>Invoke-Package -Local</c> before running any
/// smoke test. Nothing here is required to exist at resolution time —
/// callers check and surface their own build hint.
/// </remarks>
public static class EngineBundlePath
{
    /// <summary>Absolute path to the staged bundle root.</summary>
    public static string Root { get; } =
        Path.Combine(RepositoryRoot.Value, "src", "AutoContext.VsCode", "engine");

    /// <summary>Absolute path to the bundled <c>autocontext-engine</c> executable.</summary>
    public static string Executable { get; } = Path.Combine(
        Root,
        "autocontext-engine" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

    /// <summary>Absolute path to the bundled curated instructions corpus.</summary>
    public static string Instructions { get; } = Path.Combine(Root, "Instructions");

    /// <summary>Absolute path to the bundled generated resource manifests.</summary>
    public static string Resources { get; } = Path.Combine(Root, "Resources");

    /// <summary>Absolute path to the directory holding one subdirectory per worker.</summary>
    public static string Workers { get; } = Path.Combine(Root, "Workers");

    /// <summary>Absolute path to the bundled worker roster manifest.</summary>
    public static string WorkersManifest { get; } = Path.Combine(Resources, "workers.json");

    /// <summary>
    /// Throws when the bundle has not been staged, naming the packaging
    /// step that produces it.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">
    /// The bundle root does not exist.
    /// </exception>
    public static void RequireStaged()
    {
        if (!Directory.Exists(Root))
        {
            throw new DirectoryNotFoundException(
                $"Engine bundle not staged at '{Root}'. "
                + "Run '.\\scripts\\package.ps1 -Local' before running engine bundle smoke tests.");
        }
    }
}
