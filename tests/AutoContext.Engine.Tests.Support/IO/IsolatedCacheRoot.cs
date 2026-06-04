namespace AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// Allocates a per-test engine cache root under a fresh temp
/// directory. Two engines spawned with the same
/// <see cref="Path"/> via the engine's <c>--cache-root</c> CLI
/// option share a cache root — exactly what the cross-engine
/// housekeeping test needs.
/// </summary>
public sealed record class IsolatedCacheRoot(string Path)
{
    private const string EngineCacheDirName = "autocontext";

    public static IsolatedCacheRoot Create()
    {
        var parent = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "autocontext-engine-tests-cache",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);

        var cacheRootPath = System.IO.Path.Combine(parent, EngineCacheDirName);

        return new IsolatedCacheRoot(cacheRootPath);
    }
}
