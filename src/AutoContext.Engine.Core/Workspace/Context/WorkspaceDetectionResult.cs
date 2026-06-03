namespace AutoContext.Engine.Core.Workspace.Context;

using System.Collections.Frozen;

/// <summary>
/// The immutable outcome of one workspace detection pass: the full set
/// of technology <see cref="Flags"/> raised for the workspace, after the
/// base file-presence and content scans have run and the activation
/// cascade has propagated every implied parent flag. Produced by
/// <see cref="WorkspaceContextDetector.DetectAsync"/> and surfaced
/// through <see cref="WorkspaceContextDetector.Current"/>; the engine
/// projects its <see cref="Flags"/> and derived <see cref="Extensions"/>
/// onto the <c>JsonWorkspaceDetectResult</c> wire contract in a later
/// phase.
/// </summary>
/// <remarks>
/// <see cref="Flags"/> is an unordered set of raised flag names — a flag
/// is present in the set exactly when it is <see langword="true"/> on the
/// wire contract; every other flag is absent (i.e. <see langword="false"/>).
/// The set carries no value-equality contract, so callers comparing two
/// results compare the sets explicitly with
/// <see cref="IReadOnlySet{T}.SetEquals"/> rather than
/// relying on record equality.
/// </remarks>
internal sealed record WorkspaceDetectionResult
{
    /// <summary>
    /// The empty result — no flags raised. Used as the seed before the
    /// first detection pass completes.
    /// </summary>
    public static readonly WorkspaceDetectionResult Empty =
        new() { Flags = FrozenSet<string>.Empty, Extensions = [] };

    /// <summary>
    /// The distinct file extensions (e.g. <c>cs</c>, <c>ts</c>) named by
    /// the active file-rule flags, in ordinal order. Derived from the same
    /// detection pass as <see cref="Flags"/> and projected onto the
    /// <c>extensions</c> field of the wire contract; empty when no active
    /// flag names an extension.
    /// </summary>
    public required IReadOnlyList<string> Extensions { get; init; }

    /// <summary>
    /// The raised technology flag names (e.g. <c>hasCSharp</c>,
    /// <c>hasNodeJs</c>). A name's presence means the flag is set; its
    /// absence means the flag is unset.
    /// </summary>
    public required IReadOnlySet<string> Flags { get; init; }

    /// <summary>
    /// Whether <paramref name="flag"/> was raised by this detection pass.
    /// </summary>
    /// <param name="flag">The flag name to test (e.g. <c>hasReact</c>).</param>
    /// <returns><see langword="true"/> when the flag is in
    /// <see cref="Flags"/>.</returns>
    public bool Has(string flag)
        => Flags.Contains(flag);
}
