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
}
