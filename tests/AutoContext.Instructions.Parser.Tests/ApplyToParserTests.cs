namespace AutoContext.Instructions.Parser.Tests;

public sealed class ApplyToParserTests
{
    public sealed class Parse
    {
        [Fact]
        public void Should_reject_null_apply_to()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => ApplyToParser.Parse(null!));
        }

        [Fact]
        public void Should_split_comma_separated_globs_and_trim_whitespace()
        {
            // Act
            var result = ApplyToParser.Parse("  **/*.cs , **/*.ts  ");

            // Assert
            Assert.Equal(["**/*.cs", "**/*.ts"], result.Globs);
        }

        [Fact]
        public void Should_drop_empty_terms()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.cs,, ,**/*.ts");

            // Assert
            Assert.Equal(["**/*.cs", "**/*.ts"], result.Globs);
        }

        [Fact]
        public void Should_keep_intra_brace_commas_in_a_single_glob()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.{cs,fs,vb}");

            // Assert
            Assert.Equal(["**/*.{cs,fs,vb}"], result.Globs);
        }

        [Fact]
        public void Should_split_top_level_commas_while_preserving_brace_groups()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.{csproj,fsproj,vbproj},**/Directory.Packages.props");

            // Assert
            Assert.Equal(
                ["**/*.{csproj,fsproj,vbproj}", "**/Directory.Packages.props"],
                result.Globs);
        }

        [Fact]
        public void Should_expand_a_brace_group_into_individual_globs()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.{cs,fs,vb}");

            // Assert
            Assert.Equal(["**/*.cs", "**/*.fs", "**/*.vb"], result.ExpandedGlobs);
        }

        [Fact]
        public void Should_expand_multiple_brace_groups_as_a_cartesian_product()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.{test,spec}.{js,ts}");

            // Assert
            Assert.Equal(
                ["**/*.test.js", "**/*.test.ts", "**/*.spec.js", "**/*.spec.ts"],
                result.ExpandedGlobs);
        }

        [Fact]
        public void Should_pass_brace_free_globs_through_expansion_unchanged()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.cs,**/*.ts");

            // Assert
            Assert.Equal(["**/*.cs", "**/*.ts"], result.ExpandedGlobs);
        }

        [Fact]
        public void Should_extract_the_dotless_lowercase_extension_set()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.{cs,fs,vb}");

            // Assert
            Assert.Equal(["cs", "fs", "vb"], Sorted(result.Extensions));
        }

        [Fact]
        public void Should_extract_extensions_from_cartesian_expansions()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.{test,spec}.{js,jsx,ts}");

            // Assert
            Assert.Equal(["js", "jsx", "ts"], Sorted(result.Extensions));
        }

        [Fact]
        public void Should_extract_the_extension_of_a_literal_filename_glob()
        {
            // Act
            var result = ApplyToParser.Parse("**/Directory.Packages.props");

            // Assert
            Assert.Equal(["props"], Sorted(result.Extensions));
        }

        [Theory]
        [InlineData("**/*")]
        [InlineData("**")]
        [InlineData("**/Dockerfile*")]
        public void Should_extract_no_extension_when_no_concrete_extension_is_named(string applyTo)
        {
            // Act
            var result = ApplyToParser.Parse(applyTo);

            // Assert
            Assert.Empty(result.Extensions);
        }

        [Fact]
        public void Should_preserve_distinct_star_patterns_verbatim()
        {
            // Act
            var doubleStar = ApplyToParser.Parse("**");
            var doubleStarSlash = ApplyToParser.Parse("**/*");

            // Assert
            Assert.Multiple(
                () => Assert.Equal(["**"], doubleStar.Globs),
                () => Assert.Equal(["**/*"], doubleStarSlash.Globs));
        }

        [Fact]
        public void Should_take_the_last_dotted_segment_as_the_extension()
        {
            // Act
            var result = ApplyToParser.Parse("**/*.razor,**/*.razor.cs");

            // Assert
            Assert.Multiple(
                () => Assert.Equal(["**/*.razor", "**/*.razor.cs"], result.ExpandedGlobs),
                () => Assert.Equal(["cs", "razor"], Sorted(result.Extensions)));
        }

        [Fact]
        public void Should_parse_a_mixed_filename_wildcard_and_brace_corpus_value()
        {
            // Act
            var result = ApplyToParser.Parse("**/Dockerfile*,**/docker-compose*.{yml,yaml},**/.dockerignore");

            // Assert
            Assert.Multiple(
                () => Assert.Equal(
                    ["**/Dockerfile*", "**/docker-compose*.{yml,yaml}", "**/.dockerignore"],
                    result.Globs),
                () => Assert.Equal(
                    ["**/Dockerfile*", "**/docker-compose*.yml", "**/docker-compose*.yaml", "**/.dockerignore"],
                    result.ExpandedGlobs),
                () => Assert.Equal(["dockerignore", "yaml", "yml"], Sorted(result.Extensions)));
        }

        private static IReadOnlyList<string> Sorted(IReadOnlySet<string> values)
            => [.. values.OrderBy(static value => value, StringComparer.Ordinal)];
    }

    public sealed class RoundTrips
    {
        [Theory]
        [InlineData("**/*.cs")]
        [InlineData("**")]
        [InlineData("**/*")]
        [InlineData("**/*.{cs,fs,vb}")]
        [InlineData("**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}")]
        [InlineData("**/*.{csproj,fsproj,vbproj},**/Directory.Packages.props,**/nuget.config")]
        [InlineData("  **/*.cs , **/*.ts  ")]
        public void Should_round_trip_modulo_whitespace(string applyTo)
        {
            // Act
            var roundTrips = ApplyToParser.Parse(applyTo).RoundTrips;

            // Assert
            Assert.True(roundTrips);
        }

        // Every distinct `applyTo` value shipped in the instruction corpus
        // (src/AutoContext.Engine/Instructions/*.instructions.md). The
        // build-time generator enforces this same round-trip per file, so a
        // new corpus value that the parser cannot reproduce verbatim must
        // fail here first.
        [Theory]
        [InlineData("**/*.razor,**/*.razor.cs")]
        [InlineData("**/*.{cs,fs,vb}")]
        [InlineData("**/Dockerfile*,**/docker-compose*.{yml,yaml},**/.dockerignore")]
        [InlineData("**/*.{aspx,ascx,master}")]
        [InlineData("**/*.{cs,xaml}")]
        [InlineData("**/*Tests*.{cs,fs,vb,razor}")]
        [InlineData("**/*.{cs,fs,vb,proto}")]
        [InlineData("**/*.{csproj,fsproj,vbproj},**/Directory.Packages.props,**/Directory.Build.props,**/packages.config,**/nuget.config")]
        [InlineData("**/*.cs")]
        [InlineData("**/*.{cs,vb}")]
        [InlineData("**/*.{cs,vb,xaml}")]
        [InlineData("**/*.xaml")]
        [InlineData("**/*.{sh,bash}")]
        [InlineData("**/*.{bat,cmd}")]
        [InlineData("**/*.{c,h}")]
        [InlineData("**/*.{cpp,cxx,cc,h,hpp,hxx,hh}")]
        [InlineData("**/*.dart")]
        [InlineData("**/*.css")]
        [InlineData("**/*.go")]
        [InlineData("**/*.{fs,fsi}")]
        [InlineData("**/*.java")]
        [InlineData("**/*.{html,razor,cshtml}")]
        [InlineData("**/*.{groovy,gvy}")]
        [InlineData("**/*.{kt,kts}")]
        [InlineData("**/*.php")]
        [InlineData("**/*.py")]
        [InlineData("**/*.{graphql,gql}")]
        [InlineData("**/*.{ps1,psm1,psd1}")]
        [InlineData("**/*.lua")]
        [InlineData("**/*.rs")]
        [InlineData("**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}")]
        [InlineData("**/*.{scala,sc}")]
        [InlineData("**/*.rb")]
        [InlineData("**/*.sql")]
        [InlineData("**/*.swift")]
        [InlineData("**/*.{ts,tsx,mts,cts}")]
        [InlineData("**/*.vb")]
        [InlineData("**/*.{yml,yaml}")]
        [InlineData("**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts},**/*Tests*.{cs,fs,vb,razor}")]
        [InlineData("**/*.{ts,mts,cts,html}")]
        [InlineData("**/*.{test,spec,cy}.{js,jsx,ts,tsx,mjs,mts}")]
        [InlineData("**/*.{svelte,js,jsx,mjs,cjs,ts,tsx,mts,cts}")]
        [InlineData("**/*.{vue,js,jsx,mjs,cjs,ts,tsx,mts,cts}")]
        [InlineData("**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}")]
        public void Should_round_trip_every_shipped_corpus_value(string applyTo)
        {
            // Act
            var roundTrips = ApplyToParser.Parse(applyTo).RoundTrips;

            // Assert
            Assert.True(roundTrips);
        }
    }
}
