namespace AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// An xUnit class fixture that owns a single temp root for the
/// lifetime of a test class and hands out isolated paths and
/// subdirectories beneath it. The whole root is removed when xUnit
/// disposes the fixture at class teardown. For a single
/// inline-disposed temp directory (with cleanup ordered relative to
/// other resources), use <see cref="TempDirectory"/> directly.
/// </summary>
public sealed class TempDirectoryFixture : IDisposable
{
    private readonly TempDirectory _root = TempDirectory.CreateNew("ac-tests");

    /// <summary>
    /// Allocates a fresh, isolated file path under the fixture's root directory.
    /// </summary>
    public string CreatePath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var subdirectory = Path.Combine(_root.Path, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(subdirectory);
        return Path.Combine(subdirectory, fileName);
    }

    /// <summary>
    /// Allocates a fresh, isolated subdirectory under the fixture's root directory
    /// and returns its absolute path.
    /// </summary>
    public string CreateDirectory()
    {
        var subdirectory = Path.Combine(_root.Path, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(subdirectory);
        return subdirectory;
    }

    public void Dispose() =>
        _root.Dispose();
}
