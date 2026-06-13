namespace AutoContext.Workers.Manifest.Generator.Tests.Support;

/// <summary>
/// Builds a throwaway directory tree of synthetic <c>AutoContext.Worker.*</c>
/// project folders, each carrying a <c>.autocontext-worker.json</c> descriptor,
/// for the scanner and generator tests, and deletes it on dispose.
/// </summary>
internal sealed class WorkerSourceTree : IDisposable
{
    public WorkerSourceTree()
    {
        Root = Path.Combine(Path.GetTempPath(), "ac-workers-gen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Gets the root directory the synthetic worker projects live under.</summary>
    public string Root { get; }

    public WorkerSourceTree AddWorker(string projectName, string descriptorJson)
    {
        var directory = Path.Combine(Root, projectName);
        Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, ".autocontext-worker.json"), descriptorJson);

        return this;
    }

    public WorkerSourceTree AddWorkerWithoutDescriptor(string projectName)
    {
        Directory.CreateDirectory(Path.Combine(Root, projectName));

        return this;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
