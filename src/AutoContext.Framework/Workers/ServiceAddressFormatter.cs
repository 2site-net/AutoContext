namespace AutoContext.Framework.Workers;

/// <summary>
/// Formats and parses Windows named-pipe "service addresses" used to
/// reach extension-hosted services (log, health-monitor, worker-control)
/// and worker dispatcher pipes (worker-dotnet, worker-workspace,
/// worker-web). The canonical shape is
/// <c>autocontext.&lt;role&gt;#&lt;instance-id&gt;</c>; when no
/// <c>instance-id</c> is supplied (standalone runs, smoke tests) the
/// trailing <c>#</c> separator and id are omitted, leaving
/// <c>autocontext.&lt;role&gt;</c>.
/// </summary>
/// <remarks>
/// <c>#</c> is intentional and shell-safe mid-token in cmd / pwsh /
/// bash (the comment behaviour only triggers at the start of a token
/// or after whitespace). Mirrors the TypeScript
/// <c>IdentifierFactory.createServiceAddress</c> in the extension.
/// </remarks>
public static class ServiceAddressFormatter
{
    private const string Namespace = "autocontext";
    private const char NamespaceSeparator = '.';
    private const char InstanceSeparator = '#';

    /// <summary>
    /// Returns the service address for the given <paramref name="role"/>.
    /// Appends <c>#&lt;instanceId&gt;</c> when the id is non-empty.
    /// </summary>
    /// <param name="role">Service role, e.g. <c>"log"</c>, <c>"health-monitor"</c>, <c>"worker-dotnet"</c>.</param>
    /// <param name="instanceId">Per-window instance id; empty/whitespace treated as absent.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="role"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public static string Format(string role, string? instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var trimmedId = instanceId?.Trim();

        return string.IsNullOrEmpty(trimmedId)
            ? $"{Namespace}{NamespaceSeparator}{role}"
            : $"{Namespace}{NamespaceSeparator}{role}{InstanceSeparator}{trimmedId}";
    }

    /// <summary>
    /// Inverse of <see cref="Format"/>: extracts the <paramref name="role"/>
    /// segment from a service address. Strips the <c>autocontext.</c>
    /// prefix and any trailing <c>#&lt;instance-id&gt;</c> suffix.
    /// </summary>
    /// <param name="address">A service address (may or may not carry an instance id).</param>
    /// <param name="role">Extracted role on success; <see cref="string.Empty"/> on failure.</param>
    /// <returns><see langword="true"/> when the address carried the
    /// <c>autocontext.</c> namespace and a non-empty role.</returns>
    public static bool TryParseRole(string address, out string role)
    {
        if (string.IsNullOrEmpty(address))
        {
            role = string.Empty;
            return false;
        }

        const string Prefix = Namespace + ".";
        if (!address.StartsWith(Prefix, StringComparison.Ordinal))
        {
            role = string.Empty;
            return false;
        }

        var tail = address[Prefix.Length..];
        var hashIndex = tail.IndexOf(InstanceSeparator, StringComparison.Ordinal);
        var roleSegment = hashIndex < 0 ? tail : tail[..hashIndex];

        if (roleSegment.Length == 0)
        {
            role = string.Empty;
            return false;
        }

        role = roleSegment;
        return true;
    }
}
