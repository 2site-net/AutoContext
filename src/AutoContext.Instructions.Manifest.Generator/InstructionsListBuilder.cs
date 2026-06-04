namespace AutoContext.Instructions.Manifest.Generator;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using AutoContext.Instructions.Parser;

/// <summary>
/// Scans the curated instruction corpus and builds the wire-shape
/// <c>instructions-files.json</c> catalogue. The builder is the build-side
/// library named by the engine design;
/// <see cref="InstructionsManifestGenerator"/> drives it from the host entry
/// point. Frontmatter reading is delegated to the shared
/// <see cref="InstructionsFileParser"/> so build-time catalogue generation and
/// runtime parsing observe one parse of each file. The builder derives content
/// hashes and validates curatorial shape, but deliberately never inspects glob
/// semantics — <c>applyTo</c> is carried verbatim onto the wire.
/// </summary>
internal sealed partial class InstructionsListBuilder : IInstructionsListBuilder
{
    private const string InstructionsFileSuffix = ".instructions.md";
    private const string SchemaVersion = "1";

    private static readonly HashSet<string> AlwaysAttachedFiles =
        new(StringComparer.Ordinal)
        {
            "copilot.instructions.md",
            "autocontext.instructions.md",
        };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public InstructionsManifest Build(string corpusDirectory)
    {
        ArgumentNullException.ThrowIfNull(corpusDirectory);

        var fileNames = Directory
            .GetFiles(corpusDirectory, "*" + InstructionsFileSuffix)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        var entries = new List<InstructionsManifestEntry>(fileNames.Count);

        foreach (var fileName in fileNames)
        {
            entries.Add(BuildEntry(corpusDirectory, fileName));
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

        return new InstructionsManifest(SchemaVersion, entries);
    }

    private static InstructionsManifestEntry BuildEntry(string corpusDirectory, string fileName)
    {
        var content = File.ReadAllText(Path.Combine(corpusDirectory, fileName), Utf8NoBom);
        var frontmatter = InstructionsFileParser.ParseFrontmatter(content);
        var name = frontmatter.Name;

        if (name is null || name.Length == 0)
        {
            throw Fail(fileName, "missing required `name` frontmatter field");
        }

        var nameMatch = GeneratedNamePatternRegex().Match(name);

        if (!nameMatch.Success)
        {
            throw Fail(fileName, "`name` does not match `<key> (vX.Y.Z)`: '" + name + "'");
        }

        var key = nameMatch.Groups[1].Value;
        var version = nameMatch.Groups[2].Value;

        var expectedKey = fileName[..^InstructionsFileSuffix.Length];

        if (!string.Equals(key, expectedKey, StringComparison.Ordinal))
        {
            throw Fail(fileName, "`name` key '" + key + "' does not equal file basename '" + expectedKey + "'");
        }

        var description = frontmatter.Description?.Trim();

        if (description is null || description.Length == 0)
        {
            throw Fail(fileName, "missing or empty `description` frontmatter field");
        }

        var applyTo = frontmatter.ApplyTo?.RawValue;

        if (applyTo is not null && applyTo.AsSpan().Trim().IsEmpty)
        {
            throw Fail(fileName, "`applyTo` is present but empty");
        }

        var contentHash = ComputeContentHash(GeneratedFrontmatterBlockRegex().Replace(content, string.Empty));
        var hasChangelog = File.Exists(Path.Combine(corpusDirectory, expectedKey + ".CHANGELOG.md"));
        var alwaysAttached = AlwaysAttachedFiles.Contains(fileName);

        return new InstructionsManifestEntry(
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
        var hash = SHA256.HashData(Utf8NoBom.GetBytes(body));

        return "sha256:" + Convert.ToHexStringLower(hash);
    }

    private static InvalidOperationException Fail(string fileName, string message)
        => new("[" + fileName + "] " + message);

    [GeneratedRegex(@"^---\r?\n[\s\S]*?\r?\n---\r?\n?")]
    private static partial Regex GeneratedFrontmatterBlockRegex();

    [GeneratedRegex(@"^([a-z0-9][a-z0-9-]*) \(v(\d+\.\d+\.\d+)\)$")]
    private static partial Regex GeneratedNamePatternRegex();
}
