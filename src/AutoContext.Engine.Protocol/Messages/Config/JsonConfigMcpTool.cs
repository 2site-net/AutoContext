namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of one MCP tool's config state, carried on
/// <see cref="JsonConfigSnapshot.McpTools"/>.
/// </summary>
public sealed record JsonConfigMcpTool
{
    /// <summary>
    /// Tool name, or <see langword="null"/> when unknown.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Version the override was recorded against, or
    /// <see langword="null"/> when unset.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Whether the whole tool is disabled. <see langword="null"/>
    /// means "not toggled" (enabled).
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }
}
