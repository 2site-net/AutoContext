namespace AutoContext.Mcp.Server.Registry;

using System.IO;
using System.Reflection;

/// <summary>
/// Loads <c>mcp-workers-registry.json</c> from this assembly's embedded resources.
/// </summary>
public sealed class RegistryEmbeddedResource : IRegistrySource
{
    private const string EmbeddedResourceName = "AutoContext.Mcp.Server.mcp-workers-registry.json";

    public RegistryEmbeddedResource()
    {
        var assembly = typeof(RegistryEmbeddedResource).Assembly;
        Json = ReadResource(assembly, EmbeddedResourceName);
    }

    /// <inheritdoc />
    public string Json { get; }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' not found.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
