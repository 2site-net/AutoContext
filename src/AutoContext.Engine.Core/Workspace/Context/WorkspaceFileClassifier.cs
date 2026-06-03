namespace AutoContext.Engine.Core.Workspace.Context;

using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Compiles the declarative <see cref="FilePresenceRule"/> and
/// <see cref="ContentScan"/> tables into a lookup index and classifies a
/// single workspace file against it. File-presence selectors collapse
/// into extension and file-name dictionaries plus a small glob list, and
/// content scans keep their manifest selectors paired with the patterns
/// tested against the file body — so classification is a handful of
/// lookups rather than re-parsing globs per file. The matching rules are
/// the contract; this type carries the behaviour the rule records
/// deliberately do not.
/// </summary>
internal sealed class WorkspaceFileClassifier
{
    private const string HasNodeJsFlag = "hasNodeJs";
    private const string PackageJsonFileName = "package.json";

    private readonly (FrozenSet<string> Extensions, FrozenSet<string> FileNames, IReadOnlyList<ContentPatternRule> Rules)[] _contentScans;
    private readonly FrozenDictionary<string, string[]> _extensionToFlags;
    private readonly FrozenDictionary<string, string[]> _fileNameToFlags;
    private readonly (Regex Pattern, string Flag)[] _globRules;

    /// <summary>
    /// Builds the classification index from the supplied rule tables.
    /// </summary>
    /// <param name="fileRules">File-presence rules whose selectors are
    /// indexed by extension, file name, or glob.</param>
    /// <param name="contentScans">Content-scan groups whose manifest
    /// selectors and body patterns are indexed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileRules"/>
    /// or <paramref name="contentScans"/> is <see langword="null"/>.</exception>
    public WorkspaceFileClassifier(
        IReadOnlyList<FilePresenceRule> fileRules,
        IReadOnlyList<ContentScan> contentScans)
    {
        ArgumentNullException.ThrowIfNull(fileRules);
        ArgumentNullException.ThrowIfNull(contentScans);

        var extensionMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var fileNameMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var globRules = new List<(Regex, string)>();

        foreach (var rule in fileRules)
        {
            foreach (var selector in rule.Selectors)
            {
                switch (selector.Kind)
                {
                    case FileSelectorKind.Extension:
                        AddFlag(extensionMap, selector.Value, rule.Flag);
                        break;
                    case FileSelectorKind.FileName:
                        AddFlag(fileNameMap, selector.Value, rule.Flag);
                        break;
                    case FileSelectorKind.GlobPattern:
                        globRules.Add((GlobToRegex(selector.Value), rule.Flag));
                        break;
                    default:
                        break;
                }
            }
        }

        _extensionToFlags = extensionMap.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        _fileNameToFlags = fileNameMap.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        _globRules = [.. globRules];

        _contentScans =
        [
            .. contentScans.Select(static scan => (
                Extensions: scan.Selectors
                    .Where(static s => s.Kind == FileSelectorKind.Extension)
                    .Select(static s => s.Value)
                    .ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FileNames: scan.Selectors
                    .Where(static s => s.Kind == FileSelectorKind.FileName)
                    .Select(static s => s.Value)
                    .ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                scan.Rules)),
        ];
    }

    /// <summary>
    /// Classifies the file at <paramref name="fullPath"/>, returning every
    /// flag it raises by extension, file name, glob, or — for manifest
    /// files — a content-pattern match. Reads the file body at most once,
    /// and only when a content scan selects the file.
    /// </summary>
    /// <param name="fullPath">Absolute path of the file to classify.</param>
    /// <param name="relativePath">Workspace-relative, forward-slash path
    /// used for glob matching.</param>
    /// <param name="cancellationToken">Cancels the classification.</param>
    /// <returns>The flags the file raises; empty when it matches no
    /// rule.</returns>
    public async Task<HashSet<string>> ClassifyAsync(
        string fullPath, string relativePath, CancellationToken cancellationToken)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var fileName = Path.GetFileName(fullPath);
        var extension = ExtensionOf(fileName);

        if (extension.Length > 0 && _extensionToFlags.TryGetValue(extension, out var extensionFlags))
        {
            foreach (var flag in extensionFlags)
            {
                flags.Add(flag);
            }
        }

        if (_fileNameToFlags.TryGetValue(fileName, out var nameFlags))
        {
            foreach (var flag in nameFlags)
            {
                flags.Add(flag);
            }
        }

        foreach (var (pattern, flag) in _globRules)
        {
            if (pattern.IsMatch(relativePath))
            {
                flags.Add(flag);
            }
        }

        if (string.Equals(fileName, PackageJsonFileName, StringComparison.OrdinalIgnoreCase))
        {
            flags.Add(HasNodeJsFlag);
        }

        string? content = null;

        foreach (var (extensions, fileNames, rules) in _contentScans)
        {
            if (!extensions.Contains(extension) && !fileNames.Contains(fileName))
            {
                continue;
            }

            content ??= await TryReadAsync(fullPath, cancellationToken).ConfigureAwait(false);

            if (content is null)
            {
                break;
            }

            foreach (var rule in rules)
            {
                if (rule.Pattern.IsMatch(content))
                {
                    flags.Add(rule.Flag);
                }
            }
        }

        return flags;
    }

    /// <summary>
    /// Reports whether the file could raise any flag — a cheap, body-free
    /// pre-check over the same extension, file-name, glob, and content-scan
    /// selectors <see cref="ClassifyAsync"/> consults. Used to filter
    /// watcher events before queueing a reclassification.
    /// </summary>
    /// <param name="fullPath">Absolute path of the file to test.</param>
    /// <param name="relativePath">Workspace-relative, forward-slash path
    /// used for glob matching.</param>
    /// <returns><see langword="true"/> if the file matches at least one
    /// selector; otherwise <see langword="false"/>.</returns>
    public bool IsRelevant(string fullPath, string relativePath)
    {
        var fileName = Path.GetFileName(fullPath);
        var extension = ExtensionOf(fileName);

        if (extension.Length > 0 && _extensionToFlags.ContainsKey(extension))
        {
            return true;
        }

        if (_fileNameToFlags.ContainsKey(fileName))
        {
            return true;
        }

        if (string.Equals(fileName, PackageJsonFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var (extensions, fileNames, _) in _contentScans)
        {
            if (extensions.Contains(extension) || fileNames.Contains(fileName))
            {
                return true;
            }
        }

        foreach (var (pattern, _) in _globRules)
        {
            if (pattern.IsMatch(relativePath))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddFlag(Dictionary<string, List<string>> map, string key, string flag)
    {
        if (map.TryGetValue(key, out var flags))
        {
            flags.Add(flag);
        }
        else
        {
            map[key] = [flag];
        }
    }

    private static string ExtensionOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 ? fileName[(dot + 1)..] : string.Empty;
    }

    private static Regex GlobToRegex(string glob)
    {
        var normalized = glob.Replace('\\', '/');
        var pattern = new StringBuilder("^");

        for (var i = 0; i < normalized.Length;)
        {
            var c = normalized[i];

            if (c == '?')
            {
                pattern.Append("[^/]");
                i++;

                continue;
            }

            if (c != '*')
            {
                pattern.Append(Regex.Escape(c.ToString()));
                i++;

                continue;
            }

            if (i + 1 >= normalized.Length || normalized[i + 1] != '*')
            {
                pattern.Append("[^/]*");
                i++;

                continue;
            }

            if (i + 2 < normalized.Length && normalized[i + 2] == '/')
            {
                pattern.Append("(?:.*/)?");
                i += 3;
            }
            else
            {
                pattern.Append(".*");
                i += 2;
            }
        }

        pattern.Append('$');
        return new Regex(
            pattern.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static async Task<string?> TryReadAsync(string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
