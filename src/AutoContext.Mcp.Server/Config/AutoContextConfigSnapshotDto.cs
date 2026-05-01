namespace AutoContext.Mcp.Server.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Wire-shape of the disabled-state snapshot pushed by the
/// extension's <c>AutoContextConfigServer</c>. Counterpart of the
/// TypeScript <c>McpToolsDisabledSnapshot</c> in
/// <c>src/AutoContext.VsCode/src/types/mcp-tools-disabled-snapshot.ts</c>;
/// the two types must be changed together.
/// </summary>
/// <remarks>
/// Mutable POCO with explicit camelCase JSON property names so the
/// payload deserialises directly without a custom naming policy.
/// Consumed by <see cref="AutoContextConfigClient"/> and immediately
/// projected into <see cref="AutoContextConfigSnapshot"/>; downstream
/// code never sees this DTO.
/// </remarks>
internal sealed class AutoContextConfigSnapshotDto
{
    [JsonPropertyName("disabledTools")]
    public List<string> DisabledTools { get; set; } = [];

    [JsonPropertyName("disabledTasks")]
    public Dictionary<string, List<string>> DisabledTasks { get; set; } = [];
}
