namespace AutoContext.Instructions.Manifest.Generator;

using System.Security.Cryptography;
using System.Text;

using AutoContext.Instructions.Parser;

/// <summary>
/// The single disk-reading stage of one generation pass. It walks the corpus
/// directory, reads each <c>*.instructions.md</c> file once, runs one
/// <see cref="InstructionsFileParser.Parse(string)"/> over it, and precomputes the
/// frontmatter-stripped content hash and sibling-changelog flag. Every later stage
/// (<see cref="InstructionsListBuilder"/>, <see cref="InstructionsMetadataBuilder"/>,
/// and <see cref="InstructionsReferenceValidator"/>) reads the resulting
/// <see cref="ParsedCorpusFile"/> values and never touches disk again, so the markdown
/// is read and parsed exactly once.
/// </summary>
internal sealed class CorpusParser : ICorpusParser
{
    private const string InstructionsFileSuffix = ".instructions.md";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ParsedCorpusFile> Parse(string corpusDirectory)
    {
        ArgumentNullException.ThrowIfNull(corpusDirectory);

        var fileNames = Directory
            .GetFiles(corpusDirectory, "*" + InstructionsFileSuffix)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        var corpus = new Dictionary<string, ParsedCorpusFile>(fileNames.Count, StringComparer.Ordinal);

        foreach (var fileName in fileNames)
        {
            var content = File.ReadAllText(Path.Combine(corpusDirectory, fileName), Utf8NoBom);
            var parsed = InstructionsFileParser.Parse(content);
            var contentHash = ComputeContentHash(parsed.Body.RawBody);
            var key = fileName[..^InstructionsFileSuffix.Length];
            var hasChangelog = File.Exists(Path.Combine(corpusDirectory, key + ".CHANGELOG.md"));

            corpus.Add(key, new ParsedCorpusFile(fileName, content, parsed, contentHash, hasChangelog));
        }

        return corpus;
    }

    private static string ComputeContentHash(string body)
    {
        var hash = SHA256.HashData(Utf8NoBom.GetBytes(body));

        return "sha256:" + Convert.ToHexStringLower(hash);
    }
}
