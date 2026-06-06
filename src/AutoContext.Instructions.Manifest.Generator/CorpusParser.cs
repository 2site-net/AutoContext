namespace AutoContext.Instructions.Manifest.Generator;

using System.Security.Cryptography;
using System.Text;

using AutoContext.Instructions.Parser;

/// <summary>
/// The single disk-reading stage of one generation pass. It walks the corpus
/// directory, reads each <c>*.instructions.md</c> file once via
/// <see cref="InstructionsFile.Parse(string)"/>, and precomputes the
/// frontmatter-stripped content hash and sibling-changelog flag. Every later stage
/// (<see cref="InstructionsManifestBuilder"/>, <see cref="InstructionsCatalogReader"/>,
/// and <see cref="InstructionsReferenceValidator"/>) reads the resulting
/// <see cref="CorpusFileParsedResult"/> values and never touches disk again, so the markdown
/// is read and parsed exactly once.
/// </summary>
internal sealed class CorpusParser : ICorpusParser
{
    private const string InstructionsFileSuffix = ".instructions.md";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, CorpusFileParsedResult> Parse(string corpusDirectory)
    {
        ArgumentNullException.ThrowIfNull(corpusDirectory);

        var fileNames = Directory
            .GetFiles(corpusDirectory, "*" + InstructionsFileSuffix)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        var corpus = new Dictionary<string, CorpusFileParsedResult>(fileNames.Count, StringComparer.Ordinal);

        foreach (var fileName in fileNames)
        {
            var parsed = InstructionsFile.Parse(Path.Combine(corpusDirectory, fileName));
            var contentHash = ComputeContentHash(parsed.Body.RawValue);
            var key = fileName[..^InstructionsFileSuffix.Length];
            var hasChangelog = File.Exists(Path.Combine(corpusDirectory, key + ".CHANGELOG.md"));

            corpus.Add(key, new CorpusFileParsedResult(fileName, parsed, contentHash, hasChangelog));
        }

        return corpus;
    }

    private static string ComputeContentHash(string body)
    {
        var hash = SHA256.HashData(Utf8NoBom.GetBytes(body));

        return "sha256:" + Convert.ToHexStringLower(hash);
    }
}
