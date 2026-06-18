namespace AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// A uniquely-named temporary directory that deletes itself
/// (recursively, best-effort) when disposed. Integration tests stage
/// per-test workspaces, cache roots, and resource overlays under the
/// OS temp folder; wrapping each in a disposable handle keeps a test
/// run from leaving orphaned directories behind.
/// </summary>
/// <remarks>
/// Disposal swallows <see cref="IOException"/> and
/// <see cref="UnauthorizedAccessException"/>: a still-running engine
/// can briefly hold a lock on a file under the directory, and a
/// best-effort cleanup must not turn that race into a test failure.
/// Declare the handle before the engine it backs — so it disposes
/// after the engine has exited — to minimise that window.
/// </remarks>
public sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path)
    {
        Path = path;
    }

    /// <summary>Absolute path to the created temporary directory.</summary>
    public string Path { get; }

    /// <summary>
    /// Creates a new directory at
    /// <c>%TEMP%/&lt;category&gt;/&lt;guid&gt;</c> and returns a handle
    /// that deletes it on dispose.
    /// </summary>
    public static TempDirectory CreateNew(string category)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            category,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempDirectory(path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup: a peer process may still hold a lock.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup: a file under the tree may be locked or read-only.
        }
    }
}
