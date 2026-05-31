namespace AutoContext.Engine.Core.Workspace.Config;

/// <summary>
/// Immutable diagnostic preferences from the <c>diagnostic</c> block of
/// <c>.autocontext.json</c>. Pure data carried through verbatim so the
/// engine never drops a user's preferences when it rewrites the file.
/// </summary>
internal sealed record DiagnosticConfig
{
    /// <summary>
    /// When <see langword="false"/>, suppresses the warning emitted for
    /// instruction rules that lack an <c>id</c>. <see langword="null"/>
    /// when the user never set it.
    /// </summary>
    public bool? WarnOnMissingId { get; init; }
}
