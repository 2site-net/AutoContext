namespace AutoContext.Engine.Core.Workspace.Config;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Serializes a <c>mcpTools</c> value that is either an object or the
/// literal <c>false</c>. On read, a <c>false</c> token becomes
/// <see cref="JsonMcpToolConfigValue.Disabled"/> and an object token
/// becomes <see cref="JsonMcpToolConfigValue.FromEntry"/>; on write
/// the mapping is reversed, so the bare-<c>false</c> shorthand
/// survives a load/save round-trip.
/// </summary>
internal sealed class JsonMcpToolConfigValueConverter : JsonConverter<JsonMcpToolConfigValue>
{
    public override JsonMcpToolConfigValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.False)
        {
            return JsonMcpToolConfigValue.Disabled;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var entry = JsonSerializer.Deserialize<JsonMcpToolConfigEntry>(ref reader, options)
                ?? throw new JsonException("Expected an mcpTools entry object.");
            return JsonMcpToolConfigValue.FromEntry(entry);
        }

        throw new JsonException(
            $"Unexpected token '{reader.TokenType}' for an mcpTools value.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        JsonMcpToolConfigValue value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        if (value.IsShorthandDisabled)
        {
            writer.WriteBooleanValue(false);
            return;
        }

        JsonSerializer.Serialize(writer, value.Entry, options);
    }
}
