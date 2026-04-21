namespace AutoContext.Worker.Workspace.Tests._Utils;

/// <summary>
/// Test fixture that manages temporary directories and cleans them up on dispose.
/// </summary>
/// <remarks>
/// Each call to <see cref="CreateDirectory"/> creates a fresh isolated directory.
/// Cleanup on <see cref="Dispose"/> is best-effort and tolerates files still held
/// by the OS (common on Windows).
/// </remarks>
internal sealed class TempDirectoryFixture : IDisposable
{
    private readonly List<string> _directories = [];

    /// <summary>
    /// Creates a new empty temporary directory and returns its absolute path.
    /// The directory will be deleted when the fixture is disposed.
    /// </summary>
    public string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directories.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (var dir in _directories)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
