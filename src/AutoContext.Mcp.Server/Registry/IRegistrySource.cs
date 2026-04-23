namespace AutoContext.Mcp.Server.Registry;

/// <summary>
/// Provides the raw <c>mcp-workers-registry.json</c> JSON text. Decoupled from
/// any concrete loading strategy (embedded resource, disk, in-memory) so tests
/// can inject a synthetic registry.
/// </summary>
public interface IRegistrySource
{
    /// <summary>The raw, unparsed registry JSON.</summary>
    string Json { get; }
}
