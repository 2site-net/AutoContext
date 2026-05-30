namespace AutoContext.Engine.Core.Registry;

using System.Text.Json.Serialization;

using AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// On-disk envelope wrapping the persisted registry entries with a
/// schema version. Serialised and parsed exclusively by
/// <see cref="RegistryFileFormat"/>.
/// </summary>
internal sealed record JsonRegistryEnvelope(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("entries")] IReadOnlyList<JsonRegistryEntry> Entries);
