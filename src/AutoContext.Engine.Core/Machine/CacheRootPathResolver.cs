namespace AutoContext.Engine.Core.Machine;

/// <summary>
/// Resolves the per-user engine cache-root directory. The cache
/// root is the machine-wide co-owned directory described in
/// <c>design § Distributed bundle layout</c> and
/// <c>design § Engine-owned on-disk artefacts</c>; every live
/// engine on the same user account shares it.
/// </summary>
/// <remarks>
/// <para>
/// Layout per OS (no override):
/// </para>
/// <list type="bullet">
///   <item>Windows: <c>%LOCALAPPDATA%\autocontext\</c></item>
///   <item>POSIX with <c>$XDG_CACHE_HOME</c> set:
///     <c>$XDG_CACHE_HOME/autocontext/</c></item>
///   <item>POSIX without <c>$XDG_CACHE_HOME</c>:
///     <c>$HOME/.cache/autocontext/</c></item>
/// </list>
/// <para>
/// Tests and embedders override the resolved root by setting
/// <c>EngineOptions.CacheRootOverride</c> — the only knob this
/// type consults beyond the OS environment. Well-known artefact
/// paths underneath the root (logs, crash tombstones, the shared
/// liveness registry file) are composed by
/// <see cref="EngineCacheLayout"/> so each path concern is
/// resolved once and consumed from a single place.
/// </para>
/// </remarks>
internal static class CacheRootPathResolver
{
    /// <summary>Subdirectory name under the OS cache root.</summary>
    private const string CacheDirName = "autocontext";

    /// <summary>
    /// Resolves the absolute cache-root directory path. The
    /// directory is <i>not</i> created here; callers that intend to
    /// write under the root materialise it lazily on first write.
    /// </summary>
    /// <param name="overridePath">Optional library-only override
    /// (<c>EngineOptions.CacheRootOverride</c>). When non-null and
    /// non-whitespace, treated as the cache root verbatim
    /// (after <see cref="Path.GetFullPath(string)"/> normalisation).</param>
    /// <returns>Absolute cache-root path.</returns>
    /// <exception cref="InvalidOperationException">No override
    /// supplied and the OS cache-root location cannot be
    /// determined (e.g. <c>$HOME</c> unset on POSIX, or
    /// <c>%LOCALAPPDATA%</c> empty on Windows).</exception>
    public static string Resolve(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(localAppData))
            {
                throw new InvalidOperationException(
                    "Cannot resolve engine cache root: %LOCALAPPDATA% is unavailable. "
                    + "Set EngineOptions.CacheRootOverride to an absolute path.");
            }

            return Path.Combine(localAppData, CacheDirName);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");

        if (!string.IsNullOrWhiteSpace(xdg))
        {
            return Path.Combine(xdg, CacheDirName);
        }

        var home = Environment.GetEnvironmentVariable("HOME");

        if (string.IsNullOrWhiteSpace(home))
        {
            throw new InvalidOperationException(
                "Cannot resolve engine cache root: neither $XDG_CACHE_HOME nor $HOME is set. "
                + "Set EngineOptions.CacheRootOverride to an absolute path.");
        }

        return Path.Combine(home, ".cache", CacheDirName);
    }
}
