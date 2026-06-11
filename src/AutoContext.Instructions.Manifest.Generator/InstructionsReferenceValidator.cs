namespace AutoContext.Instructions.Manifest.Generator;

using AutoContext.Instructions.Parser;
using AutoContext.Instructions.Parser.Model;

/// <inheritdoc cref="IInstructionsReferenceValidator" />
internal sealed class InstructionsReferenceValidator : IInstructionsReferenceValidator
{
    /// <inheritdoc />
    public IReadOnlyList<InstructionsFileReferenceFindingEntry> Validate(IReadOnlyDictionary<string, InstructionsFileParsedFile> parsedFiles)
    {
        ArgumentNullException.ThrowIfNull(parsedFiles);

        var parsedCorpus = parsedFiles.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Content,
            StringComparer.Ordinal);
        var catalog = InstructionsFileCatalog.FromParsedCorpus(parsedCorpus);

        var findings = new List<InstructionsFileReferenceFindingEntry>();

        foreach (var key in parsedFiles.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            var file = parsedFiles[key];
            var fileFindings = InstructionsFileReferenceResolver.Resolve(
                key,
                file.Content.References,
                catalog);

            foreach (var finding in fileFindings)
            {
                findings.Add(new InstructionsFileReferenceFindingEntry(key, file.FileName, finding));
            }
        }

        return findings;
    }
}
