namespace AutoContext.Engine.Core.Infrastructure;

using System.Reflection;

/// <summary>
/// Resolves the running engine's version string from the
/// <c>AutoContext.Engine.Core</c> assembly. Centralises the single
/// spelling of "what version is this engine" so every stamp — the
/// registry entry's <c>engineVersion</c> and the
/// <c>.autocontext.json</c> <c>version</c> field — agrees.
/// </summary>
internal static class EngineVersion
{
    /// <summary>
    /// The running engine's version, resolved once on first access and
    /// cached for the process lifetime. This is the assembly's
    /// informational version — the full semver, including any
    /// build-metadata suffix.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No informational version is stamped on the assembly — a
    /// packaging or build defect. Failing fast surfaces the defect
    /// instead of emitting a fabricated version onto the wire and into
    /// <c>.autocontext.json</c>.
    /// </exception>
    public static string Value { get; } = Resolve();

    private static string Resolve()
    {
        var version = typeof(EngineVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        throw new InvalidOperationException(
            "Engine version could not be resolved: the AutoContext.Engine.Core "
            + "assembly does not carry an informational version. This indicates "
            + "a packaging or build defect.");
    }
}
