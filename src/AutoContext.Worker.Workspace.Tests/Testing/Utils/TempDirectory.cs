namespace AutoContext.Worker.Workspace.Tests.Testing.Utils;

/// <summary>
/// Per-instance temporary workspace directory, deleted (best-effort) on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string prefix)
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string WriteFile(string relativePath, string content)
    {
        var absolute = ResolveAndEnsureDirectory(relativePath);

        File.WriteAllText(absolute, content);

        return absolute;
    }

    public async Task<string> WriteFileAsync(string relativePath, string content, CancellationToken ct)
    {
        var absolute = ResolveAndEnsureDirectory(relativePath);

        await File.WriteAllTextAsync(absolute, content, ct).ConfigureAwait(false);

        return absolute;
    }

    public void Dispose()
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        try
        {
            Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string ResolveAndEnsureDirectory(string relativePath)
    {
        var absolute = Path.Combine(RootPath, relativePath);
        var directory = Path.GetDirectoryName(absolute);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return absolute;
    }
}
