namespace AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Immutable domain model for a single MCP tool parameter: the camelCase
/// <see cref="Name"/> the model passes, the JSON Schema
/// <see cref="Type"/> keyword it accepts, the model-facing
/// <see cref="Description"/>, and whether it is <see cref="Required"/>.
/// Carried in declaration order on <see cref="McpToolsRegistryTool"/>.
/// </summary>
internal sealed record McpToolsRegistryParameter
{
    /// <summary>The parameter name (camelCase).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The JSON Schema type keyword the parameter accepts (<c>string</c>,
    /// <c>number</c>, <c>boolean</c>, <c>array</c>, or <c>object</c>).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>The model-facing parameter description.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// <see langword="true"/> when the parameter is required;
    /// <see langword="false"/> when the registry omits the
    /// <c>required</c> flag.
    /// </summary>
    public required bool Required { get; init; }
}
