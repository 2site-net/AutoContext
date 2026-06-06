namespace AutoContext.Instructions.Manifest.Generator;

using AutoContext.Instructions.Parser;

/// <inheritdoc cref="IInstructionsReferenceValidator" />
internal sealed class InstructionsReferenceValidator : IInstructionsReferenceValidator
{
    /// <inheritdoc />
    public IReadOnlyList<CorpusReferenceFinding> Validate(IReadOnlyDictionary<string, ParsedCorpusFile> corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var parsedByKey = corpus.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Parsed,
            StringComparer.Ordinal);
        var catalog = InstructionsFileCatalog.FromParsed(parsedByKey);

        var findings = new List<CorpusReferenceFinding>();

        foreach (var key in corpus.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            var file = corpus[key];
            var fileFindings = InstructionsFileReferenceResolver.Resolve(
                key,
                file.Parsed.Body.References,
                catalog);

            foreach (var finding in fileFindings)
            {
                findings.Add(new CorpusReferenceFinding(key, file.FileName, finding));
            }
        }

        return findings;
    }
}
