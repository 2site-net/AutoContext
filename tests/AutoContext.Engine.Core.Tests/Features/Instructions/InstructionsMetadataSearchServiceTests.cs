namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using System.Collections.Generic;
using System.Linq;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;

public sealed class InstructionsMetadataSearchServiceTests
{
    private static readonly InstructionsFileManifestEntry Csharp =
        InstructionsFileManifestEntryTestFactory.Create(
            "lang-csharp",
            description: "C# code style and naming",
            applyTo: "**/*.cs",
            extensions: ["cs"],
            category: "Languages",
            sections:
            [
                new InstructionsSection { Heading = "Security", Anchor = "security" },
                new InstructionsSection { Heading = "Naming", Anchor = "naming" },
                new InstructionsSection { Heading = "Casing", Anchor = "naming-casing", Parent = "Naming" },
            ]) with
        { HasChangelog = true };

    private static readonly InstructionsFileManifestEntry Typescript =
        InstructionsFileManifestEntryTestFactory.Create(
            "lang-typescript",
            description: "TypeScript style guide",
            applyTo: "**/*.ts",
            extensions: ["ts"],
            category: "Frontend",
            sections: [new InstructionsSection { Heading = "Imports", Anchor = "imports" }]);

    private static readonly InstructionsFileManifestEntry Design =
        InstructionsFileManifestEntryTestFactory.Create(
            "design", description: "Design principles", category: "General");

    private static readonly IReadOnlyList<InstructionsFileManifestEntry> Corpus =
        [Csharp, Typescript, Design];

    [Fact]
    public void Should_return_every_file_when_the_predicate_is_empty()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build();

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Equal(
            ["lang-csharp", "lang-typescript", "design"],
            ok.Matches.Select(m => m.Entry.Key));
    }

    [Fact]
    public void Should_match_string_fields_by_case_insensitive_regex()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("description", "CODE STYLE"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Equal(["lang-csharp"], ok.Matches.Select(m => m.Entry.Key));
    }

    [Fact]
    public void Should_match_boolean_fields_by_equality()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("hasChangelog", true));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Equal(["lang-csharp"], ok.Matches.Select(m => m.Entry.Key));
    }

    [Fact]
    public void Should_match_section_level_numerically_and_report_matched_anchors()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("sections.level", 3));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Multiple(
            () => Assert.Equal(["lang-csharp"], ok.Matches.Select(m => m.Entry.Key)),
            () => Assert.Equal(["naming-casing"], ok.Matches[0].MatchedAnchors));
    }

    [Fact]
    public void Should_match_category_by_regex()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("category", "^Frontend$"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Equal(["lang-typescript"], ok.Matches.Select(m => m.Entry.Key));
    }

    [Fact]
    public void Should_AND_multiple_predicate_keys()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(
            ("description", "style"), ("hasChangelog", false));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Equal(["lang-typescript"], ok.Matches.Select(m => m.Entry.Key));
    }

    [Fact]
    public void Should_intersect_section_clauses_inside_one_section()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(
            ("sections.heading", "Casing"), ("sections.parent", "Naming"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Multiple(
            () => Assert.Equal(["lang-csharp"], ok.Matches.Select(m => m.Entry.Key)),
            () => Assert.Equal(["naming-casing"], ok.Matches[0].MatchedAnchors));
    }

    [Fact]
    public void Should_drop_a_file_when_no_single_section_satisfies_all_clauses()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(
            ("sections.heading", "Security"), ("sections.parent", "Naming"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Empty(ok.Matches);
    }

    [Fact]
    public void Should_match_applyTo_by_coarse_extension_intersection()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("applyTo", "src/**/*.cs"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.Equal(["lang-csharp"], ok.Matches.Select(m => m.Entry.Key));
    }

    [Fact]
    public void Should_drop_files_without_applyTo_when_an_applyTo_clause_is_present()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("applyTo", "**/*.cs"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var ok = Assert.IsType<InstructionsMetadataSearchOk>(result);
        Assert.DoesNotContain("design", ok.Matches.Select(m => m.Entry.Key));
    }

    [Fact]
    public void Should_return_unknown_field_for_an_unrecognised_key()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("bogus", "x"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var error = Assert.IsType<InstructionsMetadataSearchError>(result);
        Assert.Multiple(
            () => Assert.Equal(InstructionsMetadataSearchErrorKind.UnknownField, error.Kind),
            () => Assert.Equal("bogus", error.Field));
    }

    [Fact]
    public void Should_return_type_mismatch_when_the_value_type_is_wrong()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("hasChangelog", "true"));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var error = Assert.IsType<InstructionsMetadataSearchError>(result);
        Assert.Multiple(
            () => Assert.Equal(InstructionsMetadataSearchErrorKind.TypeMismatch, error.Kind),
            () => Assert.Equal("hasChangelog", error.Field));
    }

    [Fact]
    public void Should_return_invalid_regex_for_a_malformed_pattern()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("description", "("));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var error = Assert.IsType<InstructionsMetadataSearchError>(result);
        Assert.Equal(InstructionsMetadataSearchErrorKind.InvalidRegex, error.Kind);
    }

    [Fact]
    public void Should_return_pattern_too_long_when_a_pattern_exceeds_the_cap()
    {
        // Arrange
        var predicate = InstructionsMetadataPredicateTestFactory.Build(("description", new string('a', 257)));

        // Act
        var result = InstructionsMetadataSearchService.Evaluate(Corpus, predicate);

        // Assert
        var error = Assert.IsType<InstructionsMetadataSearchError>(result);
        Assert.Equal(InstructionsMetadataSearchErrorKind.PatternTooLong, error.Kind);
    }

    [Fact]
    public void Should_describe_every_recognised_field_in_the_schema()
        => Assert.Multiple(
            () => Assert.Contains(
                InstructionsMetadataSearchService.RecognizedFields,
                f => f.Field == "applyTo" && f.Type == "string" && f.Match == "glob"),
            () => Assert.Contains(
                InstructionsMetadataSearchService.RecognizedFields,
                f => f.Field == "hasChangelog" && f.Type == "boolean" && f.Match == "equality"),
            () => Assert.Contains(
                InstructionsMetadataSearchService.RecognizedFields,
                f => f.Field == "category" && f.Type == "string" && f.Match == "regex"),
            () => Assert.Contains(
                InstructionsMetadataSearchService.RecognizedFields,
                f => f.Field == "sections.level" && f.Type == "number" && f.Match == "equality"));
}
