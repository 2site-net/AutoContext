namespace AutoContext.Engine.Core.Tests.Testing.Fixtures;

public sealed class TempDirectoryFixture : IDisposable
{
    private readonly string _root;

    public TempDirectoryFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), $"ac-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Allocates a fresh, isolated file path under the fixture's root directory.
    /// </summary>
    public string CreatePath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var subdirectory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(subdirectory);
        return Path.Combine(subdirectory, fileName);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
