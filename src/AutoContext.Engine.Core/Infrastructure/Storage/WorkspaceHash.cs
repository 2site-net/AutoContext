namespace AutoContext.Engine.Core.Infrastructure.Storage;

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using AutoContext.Engine.Protocol;

/// <summary>
/// The 16-character uppercase-hex workspace identity hash used as
/// the <c>&lt;workspaceHash&gt;</c> segment of every
/// <see cref="Endpoint"/> and of every on-disk artefact the engine
/// owns. See <c>design § P4 (workspace identity)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Construct via <see cref="Compute(string)"/> from a workspace
/// path, or via <see cref="Parse(string, IFormatProvider?)"/> /
/// <see cref="TryParse(string?, IFormatProvider?, out WorkspaceHash)"/>
/// when round-tripping an existing identity from the wire or disk.
/// The default value is <see cref="IsEmpty"/> and cannot be used
/// for composition.
/// </para>
/// <para>
/// <see cref="Compute(string)"/> normalises only the surface form
/// of the path — case-folds on Windows, strips trailing directory
/// separators — so launchers that hand the engine the same
/// workspace through different but equivalent spellings hash to the
/// same identity. Symlinks, junctions, and 8.3 short names are
/// deliberately <b>not</b> resolved: two paths that traverse the
/// same inode via different surface names hash differently. This
/// matches the registry-mutex trade-off (see
/// <c>Registry.RegistryFileService.ComposeMutexName</c>) and avoids
/// an extra I/O hit on every endpoint composition.
/// </para>
/// </remarks>
public readonly record struct WorkspaceHash : IParsable<WorkspaceHash>
{
    private readonly string? _value;

    private WorkspaceHash(string value)
    {
        _value = value;
    }

    /// <summary>The 16-character uppercase-hex hash string, or
    /// <see cref="string.Empty"/> for the default instance.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Whether this instance is the default value
    /// (un-initialised; not safe to use for composition).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    /// <summary>
    /// Computes the workspace identity hash for
    /// <paramref name="workspacePath"/>.
    /// </summary>
    /// <param name="workspacePath">Absolute workspace path. The
    /// caller is responsible for ensuring the path is fully
    /// qualified (the engine's options validator enforces this at
    /// composition time).</param>
    /// <returns>The hash of
    /// <c>SHA-256(normalised(workspacePath))</c> truncated to
    /// <see cref="Endpoint.WorkspaceHashLength"/> characters.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="workspacePath"/> is <see langword="null"/>,
    /// empty, or whitespace.
    /// </exception>
    public static WorkspaceHash Compute(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var normalised = Normalise(workspacePath);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalised));

        // ToHexString returns uppercase, which is the canonical wire
        // form per `Endpoint.WorkspaceHash` (the parser rejects
        // lowercase hex).
        var hex = Convert.ToHexString(hash)[..Endpoint.WorkspaceHashLength];

        return new WorkspaceHash(hex);
    }

    /// <inheritdoc/>
    public static WorkspaceHash Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return TryParse(s, provider, out var result)
            ? result
            : throw new FormatException(
                $"'{s}' is not a valid workspace hash; expected {Endpoint.WorkspaceHashLength} uppercase-hex characters.");
    }

    /// <inheritdoc/>
    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out WorkspaceHash result)
    {
        if (!IsValidHash(s))
        {
            result = default;
            return false;
        }

        result = new WorkspaceHash(s);
        return true;
    }

    /// <summary>Returns <see cref="Value"/>.</summary>
    public override string ToString() => Value;

    private static bool IsValidHash([NotNullWhen(true)] string? candidate)
    {
        if (candidate is null || candidate.Length != Endpoint.WorkspaceHashLength)
        {
            return false;
        }

        foreach (var c in candidate)
        {
            var isUppercaseHex = c is (>= '0' and <= '9') or (>= 'A' and <= 'F');
            if (!isUppercaseHex)
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalise(string workspacePath)
    {
        var full = Path.GetFullPath(workspacePath);
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Preserve the root segment so "C:\" doesn't collapse to "C:"
        // on Windows or "/" to "" on POSIX after trimming.
        if (trimmed.Length == 0)
        {
            trimmed = full;
        }

        // Windows filesystems are case-insensitive; fold to a single
        // canonical case so equivalent spellings hash identically.
        // Upper-case keeps `CA1308` happy and is the same form we use
        // for the hex output below.
        return OperatingSystem.IsWindows()
            ? trimmed.ToUpperInvariant()
            : trimmed;
    }
}
