namespace AutoContext.Instructions.Parser.Tests;

public sealed class InstructionsFileFactoryTests
{
    public sealed class ParseFileAsync
    {
        [Fact]
        public async Task Should_reject_null_path()
        {
            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => InstructionsFileFactory.ParseFileAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_parse_a_file_from_disk()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\n---\n## Rules\n\n- [INST0001] **Do** a.\n";
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.instructions.md");
            await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);

            try
            {
                // Act
                var parsed = await InstructionsFileFactory.ParseFileAsync(path, TestContext.Current.CancellationToken);

                // Assert
                Assert.Multiple(
                    () => Assert.Equal(content, parsed.RawContent),
                    () => Assert.Equal("lang-csharp (v1.2.3)", parsed.Frontmatter.Name),
                    () => Assert.Equal("1.2.3", parsed.Frontmatter.Version),
                    () => Assert.Equal("INST0001", Assert.Single(parsed.Body.Rules).Id));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
