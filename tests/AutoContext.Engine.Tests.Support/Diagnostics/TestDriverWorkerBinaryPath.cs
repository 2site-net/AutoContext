namespace AutoContext.Engine.Tests.Support.Diagnostics;

/// <summary>
/// Resolves the absolute path to the
/// <c>AutoContext.Worker.Test.Driver</c> binary — the standalone,
/// deterministic stand-in worker the engine spawns when the MCP-tool
/// dispatch smoke test points it at a substitute resources tree. Mirrors
/// the sibling-output layout convention in <see cref="EngineBinaryPath"/>
/// so the engine integration suite resolves the driver the same way it
/// resolves the engine binary.
/// </summary>
/// <remarks>
/// The test project's binary output sits at
/// <c>tests/AutoContext.Engine.Tests/bin/&lt;cfg&gt;/net10.0/</c>; the
/// driver binary lives at the symmetric
/// <c>tests/AutoContext.Worker.Test.Driver/bin/&lt;cfg&gt;/net10.0/AutoContext.Worker.Test.Driver{ext}</c>
/// path, where <c>{ext}</c> is <c>.exe</c> on Windows and empty elsewhere.
/// The repository root is located by searching upward from the test binary
/// directory for the <c>AutoContext.slnx</c> solution file, so the resolver
/// does not depend on the exact number of intermediate folders. Resolution
/// is one-shot at class-load time.
/// </remarks>
public static class TestDriverWorkerBinaryPath
{
    /// <summary>
    /// Absolute path to the test-driver worker binary published next to
    /// the test project. The file is not required to exist at resolution
    /// time; callers check existence and surface a build-hint diagnostic
    /// when the file is missing.
    /// </summary>
    public static string Value { get; } = Resolve();

    private static string Resolve()
    {
        // AppContext.BaseDirectory:
        //   <repo>/tests/AutoContext.Engine.Tests/bin/<cfg>/net10.0/
        // The last two segments give us the configuration and TFM to
        // mirror into the driver project's bin folder; the repo root is
        // located by searching upward for AutoContext.slnx.
        var baseDir = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        var tfm = Path.GetFileName(baseDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(baseDir)!);
        var repoDir = RepositoryRoot.Value;
        var exeExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        return Path.Combine(
            repoDir,
            "tests",
            "AutoContext.Worker.Test.Driver",
            "bin",
            configuration,
            tfm,
            "AutoContext.Worker.Test.Driver" + exeExtension);
    }
}
