namespace AutoContext.Mcp.Tools.Hosting;

using System.IO;
using System.Reflection;

/// <summary>
/// Provides the raw <c>.mcp-tools.json</c> JSON text. Decoupled from any
/// concrete loading strategy (embedded resource, disk, in-memory) so tests
/// can inject a synthetic manifest.
/// </summary>
public interface IManifestSource
{
    /// <summary>The raw, unparsed manifest JSON.</summary>
    string Json { get; }
}

/// <summary>
/// Loads <c>.mcp-tools.json</c> from this assembly's embedded resources.
/// </summary>
public sealed class EmbeddedManifestSource : IManifestSource
{
    private const string EmbeddedResourceName = "AutoContext.Mcp.Tools.mcp-tools.json";

    public EmbeddedManifestSource()
    {
        var assembly = typeof(EmbeddedManifestSource).Assembly;
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
