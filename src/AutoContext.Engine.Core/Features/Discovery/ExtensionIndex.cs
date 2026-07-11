namespace AutoContext.Engine.Core.Features.Discovery;

using System.Text.RegularExpressions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Projects the instructions corpus into an <c>extension → instructions
/// files</c> index for prompt routing. Each file is keyed under every
/// dotless extension its <c>applyTo</c> names
/// (<see cref="InstructionsFileManifestEntry.Extensions"/>), so a prompt
/// mentioning <c>Foo.cs</c> or <c>module.psm1</c> routes to the files
/// whose <c>applyTo</c> covers that extension. Built once over the
/// immutable manifest snapshot.
/// </summary>
internal sealed partial class ExtensionIndex
{
    private readonly Dictionary<string, (string Display, IReadOnlyList<InstructionsFileManifestEntry> Files)> _filesByExtension;

    /// <summary>
    /// Builds the index over <paramref name="snapshot"/>.
    /// </summary>
    /// <param name="snapshot">The immutable instructions manifest snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/>
    /// is <see langword="null"/>.</exception>
    public ExtensionIndex(InstructionsManifestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var filesByExtension = new Dictionary<string, List<InstructionsFileManifestEntry>>(StringComparer.OrdinalIgnoreCase);
        var displayByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seenPerExtension = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in snapshot.Files)
        {
            if (file.Extensions is not { Count: > 0 } extensions)
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                if (!filesByExtension.TryGetValue(extension, out var files))
                {
                    files = [];
                    filesByExtension[extension] = files;
                    displayByExtension[extension] = "." + extension;
                    seenPerExtension[extension] = new HashSet<string>(StringComparer.Ordinal);
                }

                if (seenPerExtension[extension].Add(file.FileName))
                {
                    files.Add(file);
                }
            }
        }

        _filesByExtension = filesByExtension.ToDictionary(
            pair => pair.Key,
            pair => (displayByExtension[pair.Key], (IReadOnlyList<InstructionsFileManifestEntry>)pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scans <paramref name="prompt"/> for file extensions and returns the
    /// matched extensions together with the union of the instructions files
    /// they route to.
    /// </summary>
    /// <param name="prompt">The user prompt to scan.</param>
    /// <returns>The matched extensions (each with a leading dot, in
    /// first-seen order; only extensions that map to a file) and the
    /// routed manifest entries (de-duplicated, in corpus document
    /// order).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/>
    /// is <see langword="null"/>.</exception>
    public (IReadOnlyList<string> Extensions, IReadOnlyList<InstructionsFileManifestEntry> Files) Match(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var extensions = new List<string>();
        var extensionSeen = new HashSet<string>(StringComparer.Ordinal);
        var files = new List<InstructionsFileManifestEntry>();
        var fileSeen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ExtensionPattern().Matches(prompt))
        {
            var extension = match.Groups[1].Value;

            if (!_filesByExtension.TryGetValue(extension, out var mapped))
            {
                continue;
            }

            if (extensionSeen.Add(mapped.Display))
            {
                extensions.Add(mapped.Display);
            }

            foreach (var file in mapped.Files)
            {
                if (fileSeen.Add(file.FileName))
                {
                    files.Add(file);
                }
            }
        }

        return (extensions, files);
    }

    [GeneratedRegex(
        @"\.([A-Za-z][A-Za-z0-9]{0,12})(?=$|[^A-Za-z0-9_])",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionPattern();
}
