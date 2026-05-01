namespace AutoContext.Mcp.Server.Workers.Transport;

/// <summary>
/// Composes the transport endpoint (today: a Windows named-pipe name) from
/// a worker's short <c>id</c>. Per-window isolation suffixes are applied
/// downstream by <see cref="EndpointOptions"/>; this helper produces only
/// the base endpoint.
/// </summary>
public static class EndpointFormatter
{
    private const string PipeBaseName = "autocontext.worker";

    /// <summary>
    /// Returns the base pipe name for the given worker
    /// <paramref name="id"/> (e.g. <c>"dotnet"</c> → <c>"autocontext.worker-dotnet"</c>).
    /// </summary>
    /// <param name="id">Worker identifier (kebab-case, lowercase).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public static string Format(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return $"{PipeBaseName}-{id}";
    }

    /// <summary>
    /// Inverse of <see cref="Format"/>: extracts the short worker id
    /// from a base pipe endpoint name (i.e. one that has not yet been
    /// resolved with a <see cref="EndpointOptions"/> suffix).
    /// </summary>
    /// <param name="endpoint">Unresolved endpoint, e.g. <c>"autocontext.worker-workspace"</c>.</param>
    /// <param name="id">Extracted short id on success; <see cref="string.Empty"/> on failure.</param>
    /// <returns><see langword="true"/> when the endpoint had the
    /// expected base prefix and a non-empty id followed it.</returns>
    public static bool TryParseId(string endpoint, out string id)
    {
        if (string.IsNullOrEmpty(endpoint))
        {
            id = string.Empty;
            return false;
        }

        const string Prefix = PipeBaseName + "-";
        if (!endpoint.StartsWith(Prefix, StringComparison.Ordinal))
        {
            id = string.Empty;
            return false;
        }

        var tail = endpoint[Prefix.Length..];
        if (tail.Length == 0)
        {
            id = string.Empty;
            return false;
        }

        id = tail;
        return true;
    }
}
