namespace AutoContext.Mcp.Server.Tests.Smoke;

using System.IO;

/// <summary>
/// Resolves absolute paths to the three executables spawned by the
/// end-to-end smoke tests: <c>AutoContext.Mcp.Server</c>,
/// <c>AutoContext.Worker.DotNet</c>, and
/// <c>AutoContext.Worker.Workspace</c>.
/// </summary>
/// <remarks>
/// The test project's binary output sits at
/// <c>tests/AutoContext.Mcp.Server.Tests/bin/&lt;cfg&gt;/net10.0/</c>.
/// Each target project publishes to the symmetric
/// <c>src/&lt;project&gt;/bin/&lt;cfg&gt;/net10.0/&lt;project&gt;{ext}</c>
/// path, where <c>{ext}</c> is <c>.exe</c> on Windows and empty
/// elsewhere. We resolve configuration/TFM from this assembly's
/// <see cref="AppContext.BaseDirectory"/> and swap in the sibling
/// project name. The repository root is located by searching
/// upward for the <c>AutoContext.slnx</c> solution file, so the
/// resolver does not depend on the exact number of intermediate
/// folders.
/// </remarks>
internal static class SmokePaths
{
    internal static string McpToolsExe { get; } = ResolveExe("AutoContext.Mcp.Server");

    internal static string WorkerDotNetExe { get; } = ResolveExe("AutoContext.Worker.DotNet");

    internal static string WorkerWorkspaceExe { get; } = ResolveExe("AutoContext.Worker.Workspace");

    internal static string WorkspaceRoot { get; } = FindRepoRoot(AppContext.BaseDirectory);

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

    private static string ResolveExe(string projectName)
    {
        // AppContext.BaseDirectory:
        //   <repo>/tests/AutoContext.Mcp.Server.Tests/bin/<cfg>/net10.0/
        // The last two segments give us the configuration and TFM
        // to mirror into the target project's bin folder; the repo
        // root is located by searching upward for AutoContext.slnx.
        var baseDir = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        var tfm = Path.GetFileName(baseDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(baseDir)!);
        var repoDir = FindRepoRoot(baseDir);
        var exeExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        return Path.Combine(repoDir, "src", projectName, "bin", configuration, tfm, projectName + exeExtension);
    }
}
