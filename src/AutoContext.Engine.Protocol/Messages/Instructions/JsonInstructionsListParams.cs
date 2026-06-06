namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="InstructionsMethods.List"/> request.
/// </summary>
public sealed record JsonInstructionsListParams
{
    /// <summary>
    /// Whether each row carries its
    /// <see cref="JsonInstructionsListRow.Sections"/> index.
    /// <see langword="null"/> (the default when the param is omitted)
    /// is treated as <see langword="true"/> — the LM-tool and
    /// discovery paths need sections; tree-view callers pass
    /// <see langword="false"/> to drop the payload.
    /// </summary>
    [JsonPropertyName("includeSections")]
    public bool? IncludeSections { get; init; }

    /// <summary>
    /// Whether to drop rows whose <c>applyTo</c> extension set is
    /// disjoint from the workspace's detected extensions
    /// (<c>Workspace.Detect.extensions</c>). <see langword="null"/>
    /// (the default when omitted) is treated as <see langword="true"/>;
    /// always-attached files are exempt and never filtered out.
    /// </summary>
    [JsonPropertyName("applyToWorkspaceFilter")]
    public bool? ApplyToWorkspaceFilter { get; init; }

    /// <summary>
    /// Optional extension-only hint (e.g. <c>".ts"</c>) the engine uses
    /// to narrow rows to those whose <c>applyTo</c> matches that
    /// extension. <see langword="null"/> applies no hint-based
    /// narrowing.
    /// </summary>
    [JsonPropertyName("applyToHint")]
    public string? ApplyToHint { get; init; }
}
