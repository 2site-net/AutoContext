namespace AutoContext.Client.Core.Engine;

/// <summary>
/// Resolves the absolute path of the <c>autocontext-engine</c> binary
/// the resolver cold-spawns. Honours an explicit override; otherwise
/// probes the nested side-car path beside the running host binary
/// (<c>&lt;baseDirectory&gt;/engine/autocontext-engine[.exe]</c>), so a
/// packaged host finds its bundled engine with no PATH dependency.
/// </summary>
public sealed class EngineLocator
{
    private const string BinaryName = "autocontext-engine";
    private const string EngineSubdirectory = "engine";

    /// <summary>
    /// Returns <paramref name="engineBinaryPathOverride"/> when it is
    /// set, otherwise the bundled side-car path under
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <param name="engineBinaryPathOverride">Explicit binary path, or
    /// <see langword="null"/> to use the bundled side-car path.</param>
    public static string Resolve(string? engineBinaryPathOverride)
    {
        if (!string.IsNullOrEmpty(engineBinaryPathOverride))
        {
            return engineBinaryPathOverride;
        }

        var fileName = OperatingSystem.IsWindows() ? BinaryName + ".exe" : BinaryName;
        return Path.Combine(AppContext.BaseDirectory, EngineSubdirectory, fileName);
    }
}
