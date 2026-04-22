namespace AutoContext.Mcp.Tools.Tests.Manifest;

using System.IO;
using System.Reflection;

/// <summary>
/// Helper for loading the real <c>.mcp-tools.json</c> embedded into the test
/// assembly (see csproj <c>EmbeddedResource</c>) so happy-path tests can exercise
/// the production manifest.
/// </summary>
internal static class RealManifestFixture
{
    private const string ResourceName = "AutoContext.Mcp.Tools.Tests.real-manifest.json";

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
