namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

/// <summary>
/// Shared helpers for working with instructions files that do not belong to any one
/// parsed shape. Kept internal to the parser assembly.
/// </summary>
internal static partial class InstructionsFileUtils
{
    /// <summary>
    /// Turns a heading into its GitHub-style anchor slug: lowercased, every run of
    /// non-alphanumeric characters collapsed to a single hyphen, and leading and
    /// trailing hyphens trimmed.
    /// </summary>
    /// <param name="heading">The heading text to slugify.</param>
    /// <returns>The anchor slug.</returns>
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Anchors are lowercase by GitHub/markdown convention; this is a display slug, not a security normalization.")]
    internal static string Slugify(string heading)
    {
        var lowered = heading.ToLowerInvariant();
        var dashed = GeneratedNonSlugRunRegex().Replace(lowered, "-");

        return GeneratedEdgeHyphensRegex().Replace(dashed, string.Empty);
    }

    [GeneratedRegex("^-+|-+$")]
    private static partial Regex GeneratedEdgeHyphensRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex GeneratedNonSlugRunRegex();
}
