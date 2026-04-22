namespace AutoContext.Mcp.Tools.Manifest;

/// <summary>
/// Outcome of validating a <see cref="Manifest"/> via <see cref="ManifestValidator"/>.
/// </summary>
public sealed record ManifestValidationResult
{
    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    public ManifestValidationResult(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    /// <summary><c>true</c> when no validation errors were detected.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Ordered list of human-readable error messages.</summary>
    public IReadOnlyList<string> Errors { get; }
}
