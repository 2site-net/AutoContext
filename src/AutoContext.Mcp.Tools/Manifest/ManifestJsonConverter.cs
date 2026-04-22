namespace AutoContext.Mcp.Tools.Manifest;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Reads <see cref="Manifest"/> from JSON, treating the dynamic top-level keys
/// (other than <c>$schema</c> and <c>schemaVersion</c>) as worker names whose
/// values are arrays of <see cref="ManifestGroup"/>.
/// </summary>
internal sealed class ManifestJsonConverter : JsonConverter<Manifest>
{
    private const string SchemaKey = "$schema";
    private const string SchemaVersionKey = "schemaVersion";

    public override Manifest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Manifest root must be a JSON object; was {root.ValueKind}.");
        }

        if (!root.TryGetProperty(SchemaVersionKey, out var schemaVersionEl) ||
            schemaVersionEl.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Manifest is missing required string property 'schemaVersion'.");
        }

        var schemaVersion = schemaVersionEl.GetString()!;
        var workers = new Dictionary<string, IReadOnlyList<ManifestGroup>>(StringComparer.Ordinal);

        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, SchemaKey, StringComparison.Ordinal) ||
                string.Equals(prop.Name, SchemaVersionKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (prop.Value.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(
                    $"Worker '{prop.Name}' must be a JSON array of groups; was {prop.Value.ValueKind}.");
            }

            var groups = prop.Value.Deserialize<IReadOnlyList<ManifestGroup>>(options)
                ?? throw new JsonException($"Worker '{prop.Name}' deserialized to null.");

            workers[prop.Name] = groups;
        }

        return new Manifest
        {
            SchemaVersion = schemaVersion,
            Workers = workers,
        };
    }

    public override void Write(Utf8JsonWriter writer, Manifest value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Writing manifests is not supported.");
}
