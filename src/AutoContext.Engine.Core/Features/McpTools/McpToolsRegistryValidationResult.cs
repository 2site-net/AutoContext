namespace AutoContext.Engine.Core.Features.McpTools;

/// <summary>
/// Outcome of validating <c>mcp-tools-registry.json</c> via
/// <see cref="McpToolsRegistrySchemaValidator"/>: the ordered list of
/// human-readable failures, empty when the registry is sound.
/// </summary>
internal sealed record McpToolsRegistryValidationResult
{
    /// <summary>
    /// Creates a result over <paramref name="errors"/>.
    /// </summary>
    /// <param name="errors">The ordered failure messages; empty when the
    /// registry is valid. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/>
    /// is <see langword="null"/>.</exception>
    public McpToolsRegistryValidationResult(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        Errors = errors;
    }

    /// <summary><see langword="true"/> when no failures were detected.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>The ordered, human-readable failure messages.</summary>
    public IReadOnlyList<string> Errors { get; }
}
