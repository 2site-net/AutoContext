namespace AutoContext.Build.Tasks;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Scans the curated instruction corpus and builds the wire-shape
/// <c>instructions-files.json</c> catalogue. The builder is the build-side
/// library named by the engine design; the MSBuild task is a thin wrapper.
/// It parses frontmatter and derives content hashes, but deliberately never
/// inspects glob semantics — <c>applyTo</c> is carried verbatim onto the wire.
/// </summary>
internal static class InstructionsListBuilder
{
    private const string InstructionsFileSuffix = ".instructions.md";
    private const string SchemaVersion = "1";

    private static readonly HashSet<string> AlwaysAttachedFiles =
        new(StringComparer.Ordinal)
        {
            "copilot.instructions.md",
            "autocontext.instructions.md",
        };

    private static readonly Regex FrontmatterBlock =
        new(@"^---\r?\n[\s\S]*?\r?\n---\r?\n?", RegexOptions.Compiled);

    private static readonly Regex NamePattern =
        new(@"^([a-z0-9][a-z0-9-]*) \(v(\d+\.\d+\.\d+)\)$", RegexOptions.Compiled);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Builds the manifest from every <c>*.instructions.md</c> file in
    /// <paramref name="corpusDirectory"/>, ordered by key.
    /// </summary>
    /// <param name="corpusDirectory">The curated corpus directory.</param>
    /// <returns>The wire-shape manifest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="corpusDirectory"/>
    /// is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A corpus file has malformed
    /// or missing frontmatter.</exception>
    public static InstructionsFilesManifest Build(string corpusDirectory)
    {
        if (corpusDirectory is null)
        {
            throw new ArgumentNullException(nameof(corpusDirectory));
        }

        var fileNames = Directory
            .GetFiles(corpusDirectory, "*" + InstructionsFileSuffix)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        var entries = new List<InstructionsFileManifestEntry>(fileNames.Count);

        foreach (var fileName in fileNames)
        {
            entries.Add(BuildEntry(corpusDirectory, fileName));
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

        return new InstructionsFilesManifest(SchemaVersion, entries);
    }

    private static InstructionsFileManifestEntry BuildEntry(string corpusDirectory, string fileName)
    {
        var content = File.ReadAllText(Path.Combine(corpusDirectory, fileName), Utf8NoBom);
        var frontmatter = InstructionsFrontmatterParser.Parse(content);
        var name = frontmatter.Name;

        if (name is null || name.Length == 0)
        {
            throw Fail(fileName, "missing required `name` frontmatter field");
        }

        var nameMatch = NamePattern.Match(name);

        if (!nameMatch.Success)
        {
            throw Fail(fileName, "`name` does not match `<key> (vX.Y.Z)`: '" + name + "'");
        }

        var key = nameMatch.Groups[1].Value;
        var version = nameMatch.Groups[2].Value;

        var expectedKey = fileName.Substring(0, fileName.Length - InstructionsFileSuffix.Length);

        if (!string.Equals(key, expectedKey, StringComparison.Ordinal))
        {
            throw Fail(fileName, "`name` key '" + key + "' does not equal file basename '" + expectedKey + "'");
        }

        var description = frontmatter.Description?.Trim();

        if (description is null || description.Length == 0)
        {
            throw Fail(fileName, "missing or empty `description` frontmatter field");
        }

        var applyTo = frontmatter.ApplyTo;

        if (applyTo is not null && applyTo.Trim().Length == 0)
        {
            throw Fail(fileName, "`applyTo` is present but empty");
        }

        var contentHash = ComputeContentHash(FrontmatterBlock.Replace(content, string.Empty));
        var hasChangelog = File.Exists(Path.Combine(corpusDirectory, expectedKey + ".CHANGELOG.md"));
        var alwaysAttached = AlwaysAttachedFiles.Contains(fileName);

        return new InstructionsFileManifestEntry(
            key,
            fileName,
            name,
            version,
            description,
            applyTo,
            hasChangelog,
            contentHash,
            alwaysAttached);
    }

    private static string ComputeContentHash(string body)
    {
        using var sha256 = SHA256.Create();

        var hash = sha256.ComputeHash(Utf8NoBom.GetBytes(body));
        var builder = new StringBuilder("sha256:", 7 + (hash.Length * 2));

        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static InvalidOperationException Fail(string fileName, string message)
        => new("[" + fileName + "] " + message);
}
