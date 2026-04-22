namespace AutoContext.Mcp.Tools.Tests.Smoke;

using System.IO;

/// <summary>
/// Resolves absolute paths to the three executables spawned by the
/// end-to-end smoke tests: <c>AutoContext.Mcp.Tools</c>,
/// <c>AutoContext.Worker.DotNet</c>, and
/// <c>AutoContext.Worker.Workspace</c>.
/// </summary>
/// <remarks>
/// The test project's binary output sits at
/// <c>src/tests/AutoContext.Mcp.Tools.Tests/bin/&lt;cfg&gt;/net10.0/</c>.
/// Each target project publishes to the symmetric
/// <c>src/&lt;project&gt;/bin/&lt;cfg&gt;/net10.0/&lt;project&gt;{ext}</c>
/// path, where <c>{ext}</c> is <c>.exe</c> on Windows and empty
/// elsewhere. We resolve configuration/TFM from this assembly's
/// <see cref="AppContext.BaseDirectory"/> and swap in the sibling
/// project name.
/// </remarks>
internal static class SmokePaths
{
    internal static string McpToolsExe { get; } = ResolveExe("AutoContext.Mcp.Tools");

    internal static string WorkerDotNetExe { get; } = ResolveExe("AutoContext.Worker.DotNet");

    internal static string WorkerWorkspaceExe { get; } = ResolveExe("AutoContext.Worker.Workspace");

    internal static string WorkspaceRoot { get; } = ResolveWorkspaceRoot();

    private static string ResolveExe(string projectName)
    {
        // AppContext.BaseDirectory:
        //   <repo>/src/tests/AutoContext.Mcp.Tools.Tests/bin/<cfg>/net10.0/
        // Walk up to <repo>/src/ — five '..' levels — then down into the
        // target project's bin/<cfg>/net10.0/ folder.
        var testBinDir = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        var tfm = Path.GetFileName(testBinDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(testBinDir)!);
        var srcDir = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));
        var exeExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        return Path.Combine(srcDir, projectName, "bin", configuration, tfm, projectName + exeExtension);
    }

    private static string ResolveWorkspaceRoot()
    {
        var testBinDir = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        var srcDir = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));
        return Path.GetFullPath(Path.Combine(srcDir, ".."));
    }
}
