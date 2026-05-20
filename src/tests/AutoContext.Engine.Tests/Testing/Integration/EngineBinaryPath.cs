namespace AutoContext.Engine.Tests.Testing.Integration;

/// <summary>
/// Resolves the absolute path to the <c>autocontext-engine</c>
/// binary produced by the <c>AutoContext.Engine</c> project. Mirrors
/// the layout convention in
/// <c>src/tests/AutoContext.Mcp.Server.Tests/Smoke/SmokePaths.cs</c>
/// so the engine integration suite and the cross-process MCP smoke
/// suite resolve sibling-project outputs the same way.
/// </summary>
/// <remarks>
/// The test project's binary output sits at
/// <c>src/tests/AutoContext.Engine.Tests/bin/&lt;cfg&gt;/net10.0/</c>;
/// the engine binary lives at the symmetric
/// <c>src/AutoContext.Engine/bin/&lt;cfg&gt;/net10.0/autocontext-engine{ext}</c>
/// path, where <c>{ext}</c> is <c>.exe</c> on Windows and empty
/// elsewhere. Resolution is one-shot at class-load time.
/// </remarks>
internal static class EngineBinaryPath
{
    /// <summary>
    /// Absolute path to the engine binary published next to the
    /// test project. The file is not required to exist at
    /// resolution time; callers check existence and surface a
    /// build-hint diagnostic when the file is missing.
    /// </summary>
    internal static string Value { get; } = Resolve();

    private static string Resolve()
    {
        // AppContext.BaseDirectory:
        //   <repo>/src/tests/AutoContext.Engine.Tests/bin/<cfg>/net10.0/
        // Walk up five levels to <repo>/src/ then down into the engine
        // project's bin/<cfg>/net10.0/ folder.
        var testBinDir = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        var tfm = Path.GetFileName(testBinDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(testBinDir)!);
        var srcDir = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));
        var exeExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        return Path.Combine(srcDir, "AutoContext.Engine", "bin", configuration, tfm, "autocontext-engine" + exeExtension);
    }
}
