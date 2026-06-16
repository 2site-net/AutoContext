namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Immutable launch specification for a single child process. Carries the
/// fully resolved <see cref="Command"/> and <see cref="Arguments"/> handed
/// to the OS. Derive from this record to attach the metadata a particular
/// kind of process needs (such as a readiness marker or identifier).
/// </summary>
public record ProcessInfo
{
    /// <summary>
    /// The command-line arguments passed to <see cref="Command"/> (for
    /// example <c>--instance-id</c>, <c>--workspace-root</c>, and
    /// <c>--service</c> pairs); empty when the process takes none.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// The resolved executable (or launcher such as <c>node</c>) started
    /// for this process.
    /// </summary>
    public required string Command { get; init; }
}
