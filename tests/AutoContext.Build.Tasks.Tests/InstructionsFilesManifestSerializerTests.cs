namespace AutoContext.Build.Tasks.Tests;

using AutoContext.Build.Tasks;
using AutoContext.Build.Tasks.Tests.Support;

public sealed class InstructionsFilesManifestSerializerTests
{
    public sealed class Serialize
    {
        [Fact]
        public void Should_reject_null_manifest()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFilesManifestSerializer.Serialize(null!));
        }

        [Fact]
        public void Should_emit_schema_version_and_empty_array()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest();

            // Act
            var json = InstructionsFilesManifestSerializer.Serialize(manifest);

            // Assert
            Assert.Equal("{\n  \"schemaVersion\": \"1\",\n  \"instructions\": []\n}\n", json);
        }

        [Fact]
        public void Should_emit_entry_fields_in_canonical_order()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(applyTo: "**/*.cs", hasChangelog: true, alwaysAttached: true));

            // Act
            var json = InstructionsFilesManifestSerializer.Serialize(manifest);

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
                "      \"alwaysAttached\": true\n" +
                "    }\n" +
                "  ]\n" +
                "}\n",
                json);
        }

        [Fact]
        public void Should_omit_apply_to_field_when_null()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry(applyTo: null));

            // Act
            var json = InstructionsFilesManifestSerializer.Serialize(manifest);

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
            var json = InstructionsFilesManifestSerializer.Serialize(manifest);

            // Assert
            Assert.Contains("\"description\": \"Quote \\\" and \\\\ slash.\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_terminate_with_a_trailing_newline()
        {
            // Arrange
            var manifest = InstructionsManifestFakeData.CreateManifest(
                InstructionsManifestFakeData.CreateEntry());

            // Act
            var json = InstructionsFilesManifestSerializer.Serialize(manifest);

            // Assert
            Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        }
    }
}
