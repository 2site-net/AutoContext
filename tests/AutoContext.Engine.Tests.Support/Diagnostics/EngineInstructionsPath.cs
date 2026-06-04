namespace AutoContext.Engine.Tests.Support.Diagnostics;

/// <summary>
/// Resolves the absolute path to the curated set of instruction files shipped
/// by the <c>AutoContext.Engine</c> project
/// (<c>src/AutoContext.Engine/Instructions/</c>). Mirrors
/// <see cref="EngineBinaryPath"/>: the repository root is located by searching
/// upward from the test binary directory for the <c>AutoContext.slnx</c>
/// solution file, so the resolver does not depend on the exact number of
/// intermediate folders. Resolution is one-shot at class-load time.
/// </summary>
/// <remarks>
/// The build-time manifest generator scans this same directory; pointing the
/// builder at it from a test gives the round-trip invariant
/// (every shipped <c>applyTo</c> reproduces verbatim) a fast, build-independent
/// regression signal.
/// </remarks>
public static class EngineInstructionsPath
{
    /// <summary>
    /// Absolute path to the engine's <c>Instructions/</c> directory.
    /// The directory is not required to exist at resolution time; callers
    /// surface their own diagnostic when it is missing.
    /// </summary>
    public static string Value { get; } = Resolve();

    private static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AutoContext.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate repository root (AutoContext.slnx) starting from '{start}'.");
    }

    private static string Resolve()
    {
        var repoDir = FindRepoRoot(AppContext.BaseDirectory);

        return Path.Combine(repoDir, "src", "AutoContext.Engine", "Instructions");
    }
}
