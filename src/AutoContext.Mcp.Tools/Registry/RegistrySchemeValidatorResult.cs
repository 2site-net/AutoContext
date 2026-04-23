namespace AutoContext.Mcp.Tools.Registry;

/// <summary>
/// Outcome of validating a <see cref="McpWorkersCatalog"/> via <see cref="RegistrySchemeValidator"/>.
/// </summary>
public sealed record RegistrySchemeValidatorResult
{
    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    public RegistrySchemeValidatorResult(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    /// <summary><c>true</c> when no validation errors were detected.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Ordered list of human-readable error messages.</summary>
    public IReadOnlyList<string> Errors { get; }
}
