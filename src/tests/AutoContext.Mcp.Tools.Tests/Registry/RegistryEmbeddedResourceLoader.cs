namespace AutoContext.Mcp.Tools.Tests.Registry;

using System.IO;
using System.Reflection;

/// <summary>
/// Helper for loading the real <c>mcp-workers-registry.json</c> embedded into the
/// test assembly (see csproj <c>EmbeddedResource</c>) so happy-path tests can
/// exercise the production registry.
/// </summary>
internal static class RegistryEmbeddedResourceLoader
{
    private const string ResourceName = "mcp-workers-registry.json";

    private static readonly Lazy<string> Cached = new(LoadFromAssembly, isThreadSafe: true);

    public static string Json => Cached.Value;

    private static string LoadFromAssembly()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
