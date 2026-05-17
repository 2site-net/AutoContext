namespace AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// String constants for the registry-scoped JSON-RPC methods on
/// the engine wire. Kept in the protocol assembly so both sides
/// reference the same identifiers without copy-paste drift, and
/// grouped alongside the registry DTOs (<see cref="RegistryEntry"/>,
/// <see cref="RegistryEntriesResult"/>) they pair with.
/// </summary>
public static class RegistryMethods
{
    /// <summary>
    /// Returns the current contents of the machine-wide engine
    /// liveness registry (<c>engine-registry.json</c>) — the live
    /// peer set, including this engine's own row. Stateless,
    /// idempotent read. Defined in <c>design § RPC surface</c>.
    /// </summary>
    public const string RegistryEntries = "Engine.RegistryEntries";
}
