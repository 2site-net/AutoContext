namespace AutoContext.Engine.Protocol;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

/// <summary>
/// Canonical address of one of the engine's four logical channels, shaped
/// as <c>autocontext-engine:&lt;kind&gt;@&lt;workspaceHash&gt;#&lt;instanceId&gt;</c>
/// per <c>design § Lifecycle &gt; Endpoint</c>.
/// </summary>
/// <remarks>
/// <para>
/// The <c>&lt;workspaceHash&gt;</c> segment is the 16-char uppercase-hex
/// prefix of <c>sha256(normalisedWorkspacePath)</c> per <c>design § P4</c>;
/// the <c>&lt;instanceId&gt;</c> is the UUIDv4 the launcher minted once per
/// launcher instance. The same workspace from different launchers hashes
/// to one workspace identity but resolves to different engines (different
/// <c>&lt;instanceId&gt;</c>).
/// </para>
/// <para>
/// The transport-specific path prefix (<c>\\.\pipe\</c> on Windows when
/// the transport is a named pipe, <c>${os.tmpdir()}/</c> on POSIX) is
/// applied by the transport layer, not baked into the address. Parsing
/// and formatting here deal only with the canonical wire form, which is
/// transport-agnostic.
/// </para>
/// <para>
/// The parser validates the wire shape (prefix, separators, hash format,
/// UUID format) but does not interpret the bytes further. Semantic
/// validation (UUIDv4 version bit, hash prefix derivation) is the caller's
/// responsibility — see <c>design § P4</c>.
/// </para>
/// </remarks>
public readonly record struct Endpoint(EndpointKind Kind, string WorkspaceHash, Guid InstanceId)
    : IParsable<Endpoint>
{
    /// <summary>
    /// Length of the <c>&lt;workspaceHash&gt;</c> segment — 16 uppercase
    /// hex characters per <c>design § P4</c>.
    /// </summary>
    public const int WorkspaceHashLength = 16;

    private const char KindWorkspaceSeparator = '@';
    private const string Prefix = "autocontext-engine:";
    private const char WorkspaceInstanceSeparator = '#';

    /// <summary>
    /// Parses the canonical endpoint form. Throws on malformed input.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">The string is not a well-formed endpoint.</exception>
    public static Endpoint Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);

        return TryParse(s, provider, out var result)
            ? result
            : throw new FormatException($"'{s}' is not a well-formed endpoint.");
    }

    /// <summary>
    /// Tries to parse the canonical endpoint form.
    /// </summary>
    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out Endpoint result)
    {
        result = default;

        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        if (!s.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tail = s.AsSpan(Prefix.Length);

        var atIndex = tail.IndexOf(KindWorkspaceSeparator);
        if (atIndex <= 0 || atIndex == tail.Length - 1)
        {
            return false;
        }

        var kindSegment = tail[..atIndex];
        var afterAt = tail[(atIndex + 1)..];

        var hashIndex = afterAt.IndexOf(WorkspaceInstanceSeparator);
        if (hashIndex <= 0 || hashIndex == afterAt.Length - 1)
        {
            return false;
        }

        var workspaceSegment = afterAt[..hashIndex];
        var instanceSegment = afterAt[(hashIndex + 1)..];

        if (!TryParseKind(kindSegment, out var kind))
        {
            return false;
        }

        if (!IsValidWorkspaceHash(workspaceSegment))
        {
            return false;
        }

        if (!IsLowercaseUuidShape(instanceSegment))
        {
            return false;
        }

        if (!Guid.TryParseExact(instanceSegment, "D", out var instanceId))
        {
            return false;
        }

        result = new Endpoint(kind, workspaceSegment.ToString(), instanceId);
        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}{KindToWire(Kind)}{KindWorkspaceSeparator}{WorkspaceHash}{WorkspaceInstanceSeparator}{InstanceId:D}");

    // The canonical wire form is what `Guid.ToString("D")` emits — lowercase
    // hex, hyphenated. `Guid.TryParseExact("D", …)` is case-insensitive, so
    // we reject uppercase hex up-front to keep the parser symmetric with the
    // workspace-hash check above and to refuse non-canonical inputs.
    private static bool IsLowercaseUuidShape(ReadOnlySpan<char> segment)
    {
        foreach (var c in segment)
        {
            if (c is >= 'A' and <= 'F')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidWorkspaceHash(ReadOnlySpan<char> segment)
    {
        if (segment.Length != WorkspaceHashLength)
        {
            return false;
        }

        foreach (var c in segment)
        {
            var isUppercaseHex = c is (>= '0' and <= '9') or (>= 'A' and <= 'F');
            if (!isUppercaseHex)
            {
                return false;
            }
        }

        return true;
    }

    private static string KindToWire(EndpointKind kind)
        => kind switch
        {
            EndpointKind.Rpc => "rpc",
            EndpointKind.Events => "events",
            EndpointKind.Health => "health",
            EndpointKind.Logs => "logs",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown endpoint kind."),
        };

    private static bool TryParseKind(ReadOnlySpan<char> segment, out EndpointKind kind)
    {
        if (segment.SequenceEqual("rpc"))
        {
            kind = EndpointKind.Rpc;
            return true;
        }

        if (segment.SequenceEqual("events"))
        {
            kind = EndpointKind.Events;
            return true;
        }

        if (segment.SequenceEqual("health"))
        {
            kind = EndpointKind.Health;
            return true;
        }

        if (segment.SequenceEqual("logs"))
        {
            kind = EndpointKind.Logs;
            return true;
        }

        kind = default;
        return false;
    }
}
