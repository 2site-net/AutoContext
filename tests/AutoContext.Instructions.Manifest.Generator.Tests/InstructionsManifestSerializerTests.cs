namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Manifest.Generator.Tests.Support;

public sealed class InstructionsManifestSerializerTests
{
    public sealed class Serialize
    {
        private readonly InstructionsManifestSerializer _sut = new();

        [Fact]
        public void Should_reject_null_manifest()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Serialize(null!));
        }

        [Fact]
        public void Should_emit_schema_version_and_empty_array()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest();

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Equal("{\n  \"schemaVersion\": \"1\",\n  \"instructions\": []\n}\n", json);
        }

        [Fact]
        public void Should_emit_entry_fields_in_canonical_order()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(applyTo: "**/*.cs", hasChangelog: true));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Equal(
                "{\n" +
                "  \"schemaVersion\": \"1\",\n" +
                "  \"instructions\": [\n" +
                "    {\n" +
                "      \"key\": \"code-review\",\n" +
                "      \"fileName\": \"code-review.instructions.md\",\n" +
                "      \"name\": \"code-review (v1.0.0)\",\n" +
                "      \"version\": \"1.0.0\",\n" +
                "      \"description\": \"Apply when reviewing code.\",\n" +
                "      \"applyTo\": \"**/*.cs\",\n" +
                "      \"hasChangelog\": true,\n" +
                "      \"contentHash\": \"sha256:abc\",\n" +
                "      \"sections\": []\n" +
                "    }\n" +
                "  ]\n" +
                "}\n",
                json);
        }

        [Fact]
        public void Should_emit_extensions_after_apply_to_and_before_changelog()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(applyTo: "**/*.{cs,fs}", extensions: ["cs", "fs"]));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Multiple(
                () => Assert.Contains("\"extensions\": [\n        \"cs\",\n        \"fs\"\n      ]", json, StringComparison.Ordinal),
                () => Assert.True(
                    json.IndexOf("\"applyTo\"", StringComparison.Ordinal)
                    < json.IndexOf("\"extensions\"", StringComparison.Ordinal)),
                () => Assert.True(
                    json.IndexOf("\"extensions\"", StringComparison.Ordinal)
                    < json.IndexOf("\"hasChangelog\"", StringComparison.Ordinal)));
        }

        [Fact]
        public void Should_emit_section_rows_with_omitted_null_parent()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(
                    sections: [InstructionsManifestFakeData.CreateSection("Overview", "overview")]));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Multiple(
                () => Assert.Contains("\"sections\": [\n        {\n          \"heading\": \"Overview\",\n          \"anchor\": \"overview\"\n        }\n      ]", json, StringComparison.Ordinal),
                () => Assert.DoesNotContain("\"parent\"", json, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_omit_apply_to_field_when_null()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(applyTo: null));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.DoesNotContain("applyTo", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_escape_quotes_and_backslashes()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(description: "Quote \" and \\ slash."));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Contains("\"description\": \"Quote \\\" and \\\\ slash.\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_keep_punctuation_literal()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(description: "AutoContext's tools \u2014 see <docs> & more."));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Contains(
                "\"description\": \"AutoContext's tools \u2014 see <docs> & more.\"",
                json,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Should_terminate_with_a_trailing_newline()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry());

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        }
    }
}
