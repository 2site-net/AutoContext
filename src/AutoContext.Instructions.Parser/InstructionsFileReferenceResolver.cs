namespace AutoContext.Instructions.Parser;

using AutoContext.Instructions.Parser.Model;
using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// Resolves the bare <c>[locator#fragment]</c> references the parser captures
/// from one file against the whole-corpus <see cref="InstructionsFileCatalog"/>,
/// reporting every reference that does not point at a real rule or section. This
/// is the cross-file half of reference checking: the parser validates a
/// reference's <em>syntax</em> in isolation, this validates that it actually
/// <em>resolves</em>. The resolver is pure — it reads only the references and the
/// supplied catalog and performs no I/O — so assembling the catalog from disk
/// stays a caller concern.
/// </summary>
public static class InstructionsFileReferenceResolver
{
    private const string InstructionsFileSuffix = ".instructions.md";

    /// <summary>
    /// Resolves every reference found in one source file against the catalog.
    /// </summary>
    /// <param name="sourceKey">The catalog key of the file the references were
    /// parsed from. Same-file references (those with no locator) resolve against
    /// this key, and an explicit locator equal to it is flagged redundant.</param>
    /// <param name="references">The references the parser captured from the source
    /// file's body.</param>
    /// <param name="catalog">The whole-corpus index to resolve against.</param>
    /// <returns>One finding per reference that fails to resolve, in input order;
    /// an empty list when every reference resolves.</returns>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public static IReadOnlyList<InstructionsFileReferenceResolutionFailure> Resolve(
        string sourceKey,
        IReadOnlyList<InstructionsFileReference> references,
        InstructionsFileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(sourceKey);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(catalog);

        var findings = new List<InstructionsFileReferenceResolutionFailure>();

        foreach (var reference in references)
        {
            ResolveOne(sourceKey, reference, catalog, findings);
        }

        return findings;
    }

    private static bool IsUri(string locator)
        => locator.Contains("://", StringComparison.Ordinal)
            || locator.StartsWith("file:", StringComparison.Ordinal);

    private static string NormalizeLocator(string locator)
        => locator.EndsWith(InstructionsFileSuffix, StringComparison.Ordinal)
            ? locator[..^InstructionsFileSuffix.Length]
            : locator;

    private static void ResolveAgainstTarget(
        string targetKey,
        bool sameFile,
        InstructionsFileReference reference,
        InstructionsFileCatalog catalog,
        List<InstructionsFileReferenceResolutionFailure> findings)
    {
        if (!catalog.TryGet(targetKey, out var entry))
        {
            // A same-file miss means the caller left the source file out of the
            // catalog, not an authoring fault, so there is nothing to report.
            if (!sameFile)
            {
                findings.Add(new InstructionsFileReferenceResolutionFailure(
                    InstructionsFileReferenceFindingKind.UnknownLocator,
                    reference,
                    $"Reference locator '{targetKey}' does not match any known instructions file."));
            }

            return;
        }

        switch (reference.Address.Kind)
        {
            case InstructionsFileReferenceKind.Rule:
                ResolveRule(reference, entry, findings);
                break;
            case InstructionsFileReferenceKind.Section:
                ResolveSection(reference, entry, findings);
                break;
            default:
                break;
        }
    }

    private static void ResolveOne(
        string sourceKey,
        InstructionsFileReference reference,
        InstructionsFileCatalog catalog,
        List<InstructionsFileReferenceResolutionFailure> findings)
    {
        var locator = reference.Address.Locator;

        if (locator is null)
        {
            ResolveAgainstTarget(sourceKey, sameFile: true, reference, catalog, findings);
            return;
        }

        // URI locators are existence-unverified by design: the parser accepts the
        // syntax but the corpus catalog cannot confirm a remote target.

        if (IsUri(locator))
        {
            return;
        }

        var targetKey = NormalizeLocator(locator);

        if (string.Equals(targetKey, sourceKey, StringComparison.Ordinal))
        {
            findings.Add(new InstructionsFileReferenceResolutionFailure(
                InstructionsFileReferenceFindingKind.RedundantLocator,
                reference,
                $"Reference '{targetKey}#{reference.Address.Target}' names its own file; use the same-file form without the locator."));
        }

        ResolveAgainstTarget(targetKey, sameFile: false, reference, catalog, findings);
    }

    private static void ResolveRule(
        InstructionsFileReference reference,
        InstructionsFileCatalogEntry entry,
        List<InstructionsFileReferenceResolutionFailure> findings)
    {
        if (!entry.RuleIds.Contains(reference.Address.Target))
        {
            findings.Add(new InstructionsFileReferenceResolutionFailure(
                InstructionsFileReferenceFindingKind.DanglingRuleReference,
                reference,
                $"Rule '{reference.Address.Target}' is not defined in '{entry.Key}'."));
        }
    }

    private static void ResolveSection(
        InstructionsFileReference reference,
        InstructionsFileCatalogEntry entry,
        List<InstructionsFileReferenceResolutionFailure> findings)
    {
        var slug = InstructionsFile.Slugify(reference.Address.Target);

        var resolved = entry.Sections.Any(section =>
            string.Equals(section.Anchor, slug, StringComparison.Ordinal)
                || string.Equals(section.Heading, reference.Address.Target, StringComparison.Ordinal));

        if (!resolved)
        {
            findings.Add(new InstructionsFileReferenceResolutionFailure(
                InstructionsFileReferenceFindingKind.UnresolvedSectionReference,
                reference,
                $"Section '{reference.Address.Target}' is not defined in '{entry.Key}'."));
        }
    }
}
