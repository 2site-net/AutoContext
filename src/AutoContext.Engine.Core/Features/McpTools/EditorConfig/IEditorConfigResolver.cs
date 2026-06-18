namespace AutoContext.Engine.Core.Features.McpTools.EditorConfig;

/// <summary>
/// Resolves the EditorConfig key/value map a tool consumes before its
/// invocation is dispatched to the owning worker. Implementations hide
/// where resolution happens (the engine never resolves
/// <c>.editorconfig</c> in-process; it round-trips to
/// <c>Worker.Workspace</c>), so <see cref="IMcpToolsInvoker"/> can stay
/// unaware of that second hop and remain unit-testable with a fake.
/// </summary>
internal interface IEditorConfigResolver
{
    /// <summary>
    /// Resolves <paramref name="keys"/> for <paramref name="filePath"/>.
    /// Resolution is best-effort and never fatal: an empty
    /// <paramref name="keys"/> list, a missing
    /// <paramref name="filePath"/>, or any resolution failure yields an
    /// empty map so the caller proceeds without EditorConfig enrichment.
    /// </summary>
    /// <param name="filePath">Absolute path of the file the tool acts on,
    /// or <see langword="null"/> when the invocation carries none.</param>
    /// <param name="keys">The EditorConfig keys the tool declares, in
    /// declaration order; empty when it consumes none.</param>
    /// <param name="cancellationToken">Caller cancellation token.</param>
    /// <returns>The resolved key/value map; empty when nothing was (or
    /// could be) resolved.</returns>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string? filePath,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken);
}
