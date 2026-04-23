namespace AutoContext.Mcp.Server.Pipe;

/// <summary>
/// Runtime knob that lets <c>Mcp.Server</c> override every named-pipe
/// endpoint it opens or connects to with a caller-supplied suffix, so
/// multiple instances (two smoke-test runs, a test alongside the real
/// extension, etc.) can coexist without colliding on the well-known
/// pipe names baked into the manifest.
/// </summary>
/// <remarks>
/// When <see cref="Suffix"/> is <c>null</c> or empty, <see cref="Resolve"/>
/// returns the base name verbatim — production wiring stays byte-for-byte
/// identical to hardcoded names. Otherwise the suffix is appended with a
/// single <c>-</c> separator: <c>autocontext.dotnet-worker</c> +
/// <c>test123</c> → <c>autocontext.dotnet-worker-test123</c>.
/// </remarks>
public sealed class EndpointOptions
{
    /// <summary>
    /// Optional suffix appended (with a leading dash) to every pipe name
    /// the process opens or connects to. Defaults to <c>null</c>.
    /// </summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Returns <paramref name="baseName"/> unchanged when no suffix is
    /// configured, otherwise <c>{baseName}-{Suffix}</c>.
    /// </summary>
    public string Resolve(string baseName)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseName);

        var normalizedSuffix = Suffix?.Trim();

        return string.IsNullOrEmpty(normalizedSuffix)
            ? baseName
            : $"{baseName}-{normalizedSuffix}";
    }
}
