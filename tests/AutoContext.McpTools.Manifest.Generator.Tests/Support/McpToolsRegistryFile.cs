namespace AutoContext.McpTools.Manifest.Generator.Tests.Support;

/// <summary>
/// Writes a throwaway <c>mcp-tools-registry.json</c> into a temporary directory
/// for the projector and generator tests, exposes its path and a sibling output
/// path for the generated catalog, and deletes the directory on dispose.
/// </summary>
internal sealed class McpToolsRegistryFile : IDisposable
{
    private readonly string _root;

    public McpToolsRegistryFile(string registryJson)
    {
        _root = Path.Combine(Path.GetTempPath(), "ac-mcptools-gen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        RegistryPath = Path.Combine(_root, "mcp-tools-registry.json");
        File.WriteAllText(RegistryPath, registryJson);
    }

    /// <summary>Gets the path to the written registry file.</summary>
    public string RegistryPath { get; }

    /// <summary>Gets the path the generated <c>mcp-tools.json</c> should be written to.</summary>
    public string OutputPath => Path.Combine(_root, "mcp-tools.json");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
