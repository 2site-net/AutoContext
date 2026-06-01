namespace AutoContext.Engine.Core.Workspace.Config.Format;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Serializes a <c>mcpTools</c> value that is either an object or the
/// literal <c>false</c>. On read, a <c>false</c> token becomes
/// <see cref="JsonConfigFileMcpToolValue.Disabled"/> and an object token
/// becomes <see cref="JsonConfigFileMcpToolValue.FromEntry"/>; on write
/// the mapping is reversed, so the bare-<c>false</c> shorthand
/// survives a load/save round-trip.
/// </summary>
internal sealed class JsonConfigFileMcpToolValueConverter : JsonConverter<JsonConfigFileMcpToolValue>
{
    public override JsonConfigFileMcpToolValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.False)
        {
            return JsonConfigFileMcpToolValue.Disabled;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var entry = JsonSerializer.Deserialize<JsonConfigFileMcpToolEntry>(ref reader, options)
                ?? throw new JsonException("Expected an mcpTools entry object.");
            return JsonConfigFileMcpToolValue.FromEntry(entry);
        }

        throw new JsonException(
            $"Unexpected token '{reader.TokenType}' for an mcpTools value.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        JsonConfigFileMcpToolValue value,
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
