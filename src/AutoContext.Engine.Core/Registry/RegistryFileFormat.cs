namespace AutoContext.Engine.Core.Registry;

using System.Text.Json;
using System.Text.Json.Serialization;

using AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// Shared on-disk format helpers for <c>engine-registry.json</c>.
/// Owned jointly by <see cref="RegistryFileReader"/> and
/// <see cref="RegistryFileWriter"/> so the envelope shape, schema
/// version, JSON options, and parse logic live in exactly one
/// place. This type is stateless and has no I/O responsibilities.
/// </summary>
internal static class RegistryFileFormat
{
    /// <summary>
    /// On-disk envelope schema version emitted by every successful
    /// write. Bumped when the envelope or entry shape changes;
    /// readers of an older or newer version treat the file as
    /// empty so the writer can re-seed.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// JSON options shared by the reader and writer. CamelCase on
    /// the wire, indented for human inspection of the on-disk
    /// file.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Serialises <paramref name="entries"/> into the
    /// envelope-wrapped UTF-8 bytes that
    /// <see cref="RegistryFileWriter"/> persists to disk.
    /// </summary>
    public static byte[] Serialize(IReadOnlyList<RegistryEntry> entries) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new RegistryEnvelope(CurrentSchemaVersion, entries),
            SerializerOptions);

    /// <summary>
    /// Attempts to parse <paramref name="bytes"/> as a registry
    /// envelope. An empty input is a successful parse with no
    /// entries. A parse failure or schema-version mismatch
    /// surfaces as <see langword="false"/>; callers inspect
    /// <paramref name="onDiskVersion"/> to distinguish corruption
    /// (zero) from a known-but-unsupported version (non-zero).
    /// </summary>
    /// <param name="bytes">Raw file bytes.</param>
    /// <param name="entries">Parsed entries on success; empty on
    /// failure.</param>
    /// <param name="onDiskVersion">Envelope schema version when
    /// the JSON parsed (even if the version was wrong); zero when
    /// the JSON was empty, missing, or unparseable.</param>
    /// <returns><see langword="true"/> when the bytes parsed into
    /// the current schema (including the empty-file case);
    /// otherwise <see langword="false"/>.</returns>
    public static bool TryDeserialize(
        byte[] bytes,
        out IReadOnlyList<RegistryEntry> entries,
        out int onDiskVersion)
    {
        entries = [];
        onDiskVersion = 0;

        if (bytes.Length == 0)
        {
            return true;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<RegistryEnvelope>(bytes, SerializerOptions);
            if (envelope is null)
            {
                return false;
            }

            onDiskVersion = envelope.SchemaVersion;
            if (envelope.SchemaVersion != CurrentSchemaVersion)
            {
                return false;
            }

            entries = envelope.Entries ?? [];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record RegistryEnvelope(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("entries")] IReadOnlyList<RegistryEntry> Entries);
}
