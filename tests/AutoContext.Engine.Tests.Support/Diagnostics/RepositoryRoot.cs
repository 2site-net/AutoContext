namespace AutoContext.Engine.Tests.Support.Diagnostics;

/// <summary>
/// Locates the repository root by searching upward for the
/// <c>AutoContext.slnx</c> solution file, so path resolvers do not depend
/// on the exact number of folders between a test binary and the root.
/// </summary>
public static class RepositoryRoot
{
    /// <summary>
    /// Absolute path to the repository root containing the running test
    /// binary. Resolution is one-shot at class-load time.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No ancestor of the test binary directory contains the solution file.
    /// </exception>
    public static string Value { get; } = Resolve();

    private static string Resolve()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AutoContext.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            "Could not locate repository root (AutoContext.slnx) starting from "
            + $"'{AppContext.BaseDirectory}'.");
    }
}
