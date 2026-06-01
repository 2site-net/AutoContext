namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of one MCP task's config state, carried on
/// <see cref="JsonConfigMcpTool.Tasks"/>.
/// </summary>
public sealed record JsonConfigMcpTask
{
    /// <summary>
    /// Task name, or <see langword="null"/> when unknown.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Whether the task is disabled. <see langword="null"/> means
    /// "not toggled" (enabled).
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }
}
