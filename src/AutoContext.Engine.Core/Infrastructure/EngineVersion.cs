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
    /// The assembly's informational version (the full semver, including
    /// any build-metadata suffix), falling back to the assembly version
    /// and finally <c>"0.0.0"</c> when neither is present.
    /// </summary>
    /// <returns>The resolved version string.</returns>
    public static string Resolve()
    {
        var assembly = typeof(EngineVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
