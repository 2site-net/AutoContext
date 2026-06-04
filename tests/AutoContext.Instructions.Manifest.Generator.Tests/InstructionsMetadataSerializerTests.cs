namespace AutoContext.Instructions.Manifest.Generator.Tests;

using AutoContext.Instructions.Manifest.Generator;
using AutoContext.Instructions.Manifest.Generator.Tests.Support;

public sealed class InstructionsMetadataSerializerTests
{
    public sealed class Serialize
    {
        private readonly InstructionsMetadataSerializer _sut = new();

        [Fact]
        public void Should_reject_null_metadata()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Serialize(null!));
        }

        [Fact]
        public void Should_emit_schema_version_and_empty_array()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata();

            // Act
            var json = _sut.Serialize(metadata);

            // Assert
            Assert.Equal("{\n  \"schemaVersion\": \"1\",\n  \"instructions\": []\n}\n", json);
        }

        [Fact]
        public void Should_emit_entry_fields_in_canonical_order()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry(
                    applyTo: "**/*.cs",
                    extensions: ["cs"],
                    hasChangelog: true,
                    sections:
                    [
                        InstructionsManifestFakeData.CreateSection("Overview", "overview"),
                        InstructionsManifestFakeData.CreateSection("Details", "overview-details", parent: "Overview"),
                    ]));

            // Act
            var json = _sut.Serialize(metadata);

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
                "      \"extensions\": [\n" +
                "        \"cs\"\n" +
                "      ],\n" +
                "      \"hasChangelog\": true,\n" +
                "      \"contentHash\": \"sha256:abc\",\n" +
                "      \"sections\": [\n" +
                "        {\n" +
                "          \"heading\": \"Overview\",\n" +
                "          \"anchor\": \"overview\"\n" +
                "        },\n" +
                "        {\n" +
                "          \"heading\": \"Details\",\n" +
                "          \"anchor\": \"overview-details\",\n" +
                "          \"parent\": \"Overview\"\n" +
                "        }\n" +
                "      ]\n" +
                "    }\n" +
                "  ]\n" +
                "}\n",
                json);
        }

        [Fact]
        public void Should_emit_empty_sections_array_when_file_has_no_sections()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry());

            // Act
            var json = _sut.Serialize(metadata);

            // Assert
            Assert.Contains("\"sections\": []", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_omit_apply_to_field_when_null()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry(applyTo: null));

            // Act
            var json = _sut.Serialize(metadata);

            // Assert
            Assert.DoesNotContain("applyTo", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_omit_extensions_field_when_null()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry(applyTo: null, extensions: null));

            // Act
            var json = _sut.Serialize(metadata);

            // Assert
            Assert.DoesNotContain("extensions", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_emit_empty_extensions_array_when_apply_to_names_no_extension()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry(applyTo: "**/*", extensions: []));

            // Act
            var json = _sut.Serialize(metadata);

            // Assert
            Assert.Contains("\"extensions\": []", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_omit_parent_field_when_null()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry(
                    sections: [InstructionsManifestFakeData.CreateSection("Overview", "overview")]));

            // Act
            var json = _sut.Serialize(metadata);

            // Assert
            Assert.DoesNotContain("parent", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_keep_punctuation_literal()
        {
            // Arrange
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry(description: "AutoContext's tools \u2014 see <docs> & more."));

            // Act
            var json = _sut.Serialize(metadata);

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
            var metadata = InstructionsManifestFakeData.CreateMetadata(
                InstructionsManifestFakeData.CreateMetadataEntry());

            // Act
            var json = _sut.Serialize(metadata);

            // Assert
            Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        }
    }
}
