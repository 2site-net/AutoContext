namespace AutoContext.Engine.Protocol.Messages.Registry;

using System.Text.Json.Serialization;

/// <summary>
/// Result body for <c>Engine.RegistryEntries</c>. Carries the
/// snapshot of the machine-wide engine-liveness registry that the
/// engine read at the moment the request arrived. Each item in
/// <see cref="Entries"/> is the on-disk <see cref="JsonRegistryEntry"/>
/// shape; the on-disk file and this wire result share the same
/// value type by design (see <c>design § RPC surface</c>).
/// </summary>
public sealed record JsonRegistryEntriesResult
{
    /// <summary>
    /// All live registry rows known at the moment the engine
    /// served the request, including this engine's own row.
    /// Empty when the registry file is absent, empty, or
    /// unreadable.
    /// </summary>
    [JsonPropertyName("entries")]
    public IReadOnlyList<JsonRegistryEntry> Entries { get; init; } = [];
}
