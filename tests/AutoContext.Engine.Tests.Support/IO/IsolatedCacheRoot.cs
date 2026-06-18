namespace AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// Allocates a per-test engine cache root under a fresh temp
/// directory and deletes the whole temp subtree on dispose. Two
/// engines spawned with the same <see cref="Path"/> via the engine's
/// <c>--cache-root</c> CLI option share a cache root — exactly what
/// the cross-engine housekeeping test needs.
/// </summary>
public sealed class IsolatedCacheRoot : IDisposable
{
    private const string EngineCacheDirName = "autocontext";

    private readonly TempDirectory _root;

    private IsolatedCacheRoot(TempDirectory root)
    {
        _root = root;
        Path = System.IO.Path.Combine(root.Path, EngineCacheDirName);
    }

    /// <summary>
    /// Absolute path to the cache root passed to the engine's
    /// <c>--cache-root</c> option. The engine creates this directory on
    /// start; the parent temp subtree is removed on dispose.
    /// </summary>
    public string Path { get; }

    public static IsolatedCacheRoot Create() =>
        new(TempDirectory.CreateNew("autocontext-engine-tests-cache"));

    public void Dispose() =>
        _root.Dispose();
}
