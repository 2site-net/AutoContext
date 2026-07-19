namespace AutoContext.Engine.Core.Features.Instructions;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

using AutoContext.Instructions.Parser;

/// <summary>
/// Evaluates a free-form metadata predicate against the corpus manifest,
/// backing the <c>Instructions.SearchByMetadata</c> pipe RPC and the
/// <c>search_instructions_by_metadata</c> stdio tool through one shared
/// evaluator. Ported from the reference TS engine so both engine surfaces
/// answer identically.
/// </summary>
/// <remarks>
/// <para>Semantics:</para>
/// <list type="bullet">
///   <item>String fields (<c>name</c>, <c>key</c>, <c>fileName</c>,
///   <c>description</c>, <c>version</c>, <c>category</c>, <c>sections.*</c> text)
///   are matched by case-insensitive regex (pattern capped at 256 chars).</item>
///   <item><c>applyTo</c> is matched by workspace glob — coarse extension-set
///   intersection via <see cref="FrontmatterApplyToParser"/>, never regex (the
///   fine path match is the client's job per the coarse/fine split).</item>
///   <item><c>hasChangelog</c> is boolean exact equality; <c>sections.level</c>
///   is numeric exact equality.</item>
///   <item>Clauses are ANDed across keys; an empty predicate matches every
///   file. <c>sections.*</c> clauses are intersected per file — a single
///   section must satisfy them all, and its anchor is reported in
///   <see cref="InstructionsMetadataMatch.MatchedAnchors"/>.</item>
/// </list>
/// <para>Predicate faults (unknown field, wrong value type, invalid or
/// over-long regex) are returned as an
/// <see cref="InstructionsMetadataSearchError"/>, never thrown.</para>
/// </remarks>
internal static class InstructionsMetadataSearchService
{
    private const string GlobMatch = "glob";
    private const int MaxRegexPatternLength = 256;
    private const string RegexMatch = "regex";

    private static readonly Dictionary<string, (string JsonType, string Match)> FieldSpecs =
        new(StringComparer.Ordinal)
        {
            ["name"] = ("string", RegexMatch),
            ["key"] = ("string", RegexMatch),
            ["fileName"] = ("string", RegexMatch),
            ["description"] = ("string", RegexMatch),
            ["version"] = ("string", RegexMatch),
            ["applyTo"] = ("string", GlobMatch),
            ["category"] = ("string", RegexMatch),
            ["hasChangelog"] = ("boolean", "equality"),
            ["sections.heading"] = ("string", RegexMatch),
            ["sections.anchor"] = ("string", RegexMatch),
            ["sections.parent"] = ("string", RegexMatch),
            ["sections.level"] = ("number", "equality"),
        };

    /// <summary>
    /// The frozen schema of every recognised predicate field, attached to
    /// every <see cref="InstructionsMetadataSearchError"/> so the caller can
    /// correct an invalid predicate without a second lookup.
    /// </summary>
    public static IReadOnlyList<InstructionsMetadataFieldDescriptor> RecognizedFields { get; } =
        BuildRecognizedFields();

    /// <summary>
    /// Evaluates <paramref name="predicate"/> against
    /// <paramref name="entries"/> and returns the matched files, or a
    /// structured fault when the predicate is invalid.
    /// </summary>
    /// <param name="entries">The corpus manifest entries to match.</param>
    /// <param name="predicate">The predicate object; <see langword="null"/>,
    /// non-object, or empty matches every file.</param>
    /// <returns>An <see cref="InstructionsMetadataSearchOk"/> with the matched
    /// files, or an <see cref="InstructionsMetadataSearchError"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsMetadataSearchResult Evaluate(
        IReadOnlyList<InstructionsFileManifestEntry> entries,
        JsonElement? predicate)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var clauses = ReadClauses(predicate);

        if (Validate(clauses) is { } fault)
        {
            return fault;
        }

        var regexByField = CompileRegexes(clauses);

        var scalarClauses = new List<JsonProperty>();
        var sectionClauses = new List<JsonProperty>();
        JsonProperty? applyToClause = null;

        foreach (var clause in clauses)
        {
            if (clause.Name.StartsWith("sections.", StringComparison.Ordinal))
            {
                sectionClauses.Add(clause);
            }
            else
            {
                if (string.Equals(clause.Name, "applyTo", StringComparison.Ordinal))
                {
                    applyToClause = clause;
                }
                else
                {
                    scalarClauses.Add(clause);
                }
            }
        }

        var matches = new List<InstructionsMetadataMatch>();

        foreach (var entry in entries)
        {
            var match = MatchEntry(entry, scalarClauses, applyToClause, sectionClauses, regexByField);

            if (match is not null)
            {
                matches.Add(match);
            }
        }

        return new InstructionsMetadataSearchOk(matches);
    }

    private static List<InstructionsMetadataFieldDescriptor> BuildRecognizedFields()
    {
        var fields = new List<InstructionsMetadataFieldDescriptor>(FieldSpecs.Count);

        foreach (var (field, spec) in FieldSpecs)
        {
            fields.Add(new InstructionsMetadataFieldDescriptor(field, spec.JsonType, spec.Match));
        }

        return fields;
    }

    private static Dictionary<string, Regex> CompileRegexes(IReadOnlyList<JsonProperty> clauses)
    {
        var regexByField = new Dictionary<string, Regex>(StringComparer.Ordinal);

        foreach (var clause in clauses)
        {
            if (FieldSpecs.TryGetValue(clause.Name, out var spec)
                && spec.Match == RegexMatch
                && clause.Value.GetString() is { } pattern)
            {
                regexByField[clause.Name] = new Regex(
                    pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            }
        }

        return regexByField;
    }

    private static List<string> IntersectSectionClauses(
        IReadOnlyList<InstructionsSection> sections,
        IReadOnlyList<JsonProperty> clauses,
        IReadOnlyDictionary<string, Regex> regexByField)
    {
        var anchors = new List<string>();

        foreach (var section in sections)
        {
            if (SatisfiesAllSectionClauses(section, clauses, regexByField))
            {
                anchors.Add(section.Anchor);
            }
        }

        return anchors;
    }

    private static string JsonTypeName(JsonValueKind kind)
    {
        return kind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True => "boolean",
            JsonValueKind.False => "boolean",
            JsonValueKind.Array => "array",
            JsonValueKind.Null => "null",
            JsonValueKind.Object => "object",
            JsonValueKind.Undefined => "object",
            _ => "object",
        };
    }

    private static InstructionsMetadataMatch? MatchEntry(
        InstructionsFileManifestEntry entry,
        IReadOnlyList<JsonProperty> scalarClauses,
        JsonProperty? applyToClause,
        List<JsonProperty> sectionClauses,
        IReadOnlyDictionary<string, Regex> regexByField)
    {
        if (!MatchesScalarClauses(entry, scalarClauses, regexByField))
        {
            return null;
        }

        if (applyToClause is { } clause && !MatchesApplyTo(entry, clause.Value.GetString()))
        {
            return null;
        }

        if (sectionClauses.Count == 0)
        {
            return new InstructionsMetadataMatch(entry, null);
        }

        var anchors = IntersectSectionClauses(entry.Sections, sectionClauses, regexByField);

        if (anchors.Count == 0)
        {
            return null;
        }

        return new InstructionsMetadataMatch(entry, anchors);
    }

    private static bool MatchesApplyTo(InstructionsFileManifestEntry entry, string? userGlob)
    {
        if (string.IsNullOrWhiteSpace(userGlob)
            || entry.ApplyTo is null
            || entry.Extensions is not { Count: > 0 } fileExtensions)
        {
            return false;
        }

        var userExtensions = FrontmatterApplyToParser.Parse(userGlob).Extensions;

        if (userExtensions.Count == 0)
        {
            return false;
        }

        foreach (var extension in fileExtensions)
        {
            if (userExtensions.Contains(extension))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesScalarClause(
        InstructionsFileManifestEntry entry,
        JsonProperty clause,
        IReadOnlyDictionary<string, Regex> regexByField)
    {
        if (string.Equals(clause.Name, "hasChangelog", StringComparison.Ordinal))
        {
            return entry.HasChangelog == clause.Value.GetBoolean();
        }

        var value = ReadScalarString(entry, clause.Name);

        if (value is null)
        {
            return false;
        }

        return regexByField.TryGetValue(clause.Name, out var regex) && SafeIsMatch(regex, value);
    }

    private static bool MatchesScalarClauses(
        InstructionsFileManifestEntry entry,
        IReadOnlyList<JsonProperty> clauses,
        IReadOnlyDictionary<string, Regex> regexByField)
    {
        foreach (var clause in clauses)
        {
            if (!MatchesScalarClause(entry, clause, regexByField))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSectionClause(
        InstructionsSection section,
        JsonProperty clause,
        IReadOnlyDictionary<string, Regex> regexByField)
    {
        var subField = clause.Name["sections.".Length..];

        if (string.Equals(subField, "level", StringComparison.Ordinal))
        {
            var level = section.Parent is null ? 2 : 3;
            return clause.Value.TryGetInt32(out var expected) && level == expected;
        }

        var value = subField switch
        {
            "heading" => section.Heading,
            "anchor" => section.Anchor,
            "parent" => section.Parent,
            _ => null,
        };

        if (value is null)
        {
            return false;
        }

        return regexByField.TryGetValue(clause.Name, out var regex) && SafeIsMatch(regex, value);
    }

    private static List<JsonProperty> ReadClauses(JsonElement? predicate)
    {
        if (predicate is not { ValueKind: JsonValueKind.Object } element)
        {
            return [];
        }

        var clauses = new List<JsonProperty>();

        foreach (var property in element.EnumerateObject())
        {
            clauses.Add(property);
        }

        return clauses;
    }

    private static string? ReadScalarString(InstructionsFileManifestEntry entry, string field)
    {
        return field switch
        {
            "name" => entry.Name,
            "key" => entry.Key,
            "fileName" => entry.FileName,
            "description" => entry.Description,
            "version" => entry.Version,
            "category" => entry.Category,
            _ => null,
        };
    }

    private static InstructionsMetadataSearchError? RegexFault(JsonProperty clause)
    {
        var pattern = clause.Value.GetString() ?? string.Empty;

        if (pattern.Length > MaxRegexPatternLength)
        {
            return new InstructionsMetadataSearchError(
                InstructionsMetadataSearchErrorKind.PatternTooLong,
                clause.Name,
                $"Pattern length {pattern.Length} exceeds cap of {MaxRegexPatternLength} characters.");
        }

        try
        {
            _ = Regex.Match(string.Empty, pattern, RegexOptions.IgnoreCase);
            return null;
        }
        catch (ArgumentException ex)
        {
            return new InstructionsMetadataSearchError(
                InstructionsMetadataSearchErrorKind.InvalidRegex, clause.Name, ex.Message);
        }
    }

    private static bool SafeIsMatch(Regex regex, string value)
    {
        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool SatisfiesAllSectionClauses(
        InstructionsSection section,
        IReadOnlyList<JsonProperty> clauses,
        IReadOnlyDictionary<string, Regex> regexByField)
    {
        foreach (var clause in clauses)
        {
            if (!MatchesSectionClause(section, clause, regexByField))
            {
                return false;
            }
        }

        return true;
    }

    private static InstructionsMetadataSearchError? TypeFault(JsonProperty clause, string expected)
    {
        var actual = JsonTypeName(clause.Value.ValueKind);

        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return null;
        }

        return new InstructionsMetadataSearchError(
            InstructionsMetadataSearchErrorKind.TypeMismatch,
            clause.Name,
            $"Field '{clause.Name}' expects {expected}, got {actual}.");
    }

    private static InstructionsMetadataSearchError? Validate(IReadOnlyList<JsonProperty> clauses)
    {
        foreach (var clause in clauses)
        {
            if (!FieldSpecs.TryGetValue(clause.Name, out var spec))
            {
                return new InstructionsMetadataSearchError(
                    InstructionsMetadataSearchErrorKind.UnknownField,
                    clause.Name,
                    $"Unknown predicate field '{clause.Name}'.");
            }

            if (TypeFault(clause, spec.JsonType) is { } typeFault)
            {
                return typeFault;
            }

            if (spec.Match == RegexMatch && RegexFault(clause) is { } regexFault)
            {
                return regexFault;
            }
        }

        return null;
    }
}
