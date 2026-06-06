namespace AutoContext.Instructions.Parser.Tests;

using AutoContext.Engine.Tests.Support.IO;

public sealed class InstructionsFileTests
{
    public sealed class Parse(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_reject_a_null_path()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFile.Parse(null!));
        }

        [Fact]
        public void Should_read_and_parse_an_existing_file()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\n---\n## Heading\n\nBody.\n";
            var path = tempDirectory.CreatePath("lang-csharp.instructions.md");
            File.WriteAllText(path, content);

            // Act
            var result = InstructionsFile.Parse(path);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(content, result.RawContent),
                () => Assert.Equal("lang-csharp (v1.2.3)", result.Frontmatter.Name));
        }

        [Fact]
        public void Should_throw_when_the_file_does_not_exist()
        {
            // Arrange
            var path = tempDirectory.CreatePath("absent.instructions.md");

            // Act + Assert
            Assert.Throws<FileNotFoundException>(() => InstructionsFile.Parse(path));
        }
    }

    public sealed class ParseAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_reject_a_null_path()
        {
            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => InstructionsFile.ParseAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_read_and_parse_an_existing_file()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\n---\n## Heading\n\nBody.\n";
            var path = tempDirectory.CreatePath("lang-csharp.instructions.md");
            await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);

            // Act
            var result = await InstructionsFile.ParseAsync(path, TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(content, result.RawContent),
                () => Assert.Equal("lang-csharp (v1.2.3)", result.Frontmatter.Name));
        }

        [Fact]
        public async Task Should_throw_when_the_file_does_not_exist()
        {
            // Arrange
            var path = tempDirectory.CreatePath("absent.instructions.md");

            // Act + Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => InstructionsFile.ParseAsync(path, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_honour_a_cancelled_token()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\ndescription: \"d\"\n---\nBody.\n";
            var path = tempDirectory.CreatePath("cancel.instructions.md");
            await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            // Act + Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => InstructionsFile.ParseAsync(path, cancellation.Token));
        }
    }

    public sealed class TryParse(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_reject_a_null_path()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFile.TryParse(null!, out _));
        }

        [Fact]
        public void Should_read_and_parse_an_existing_file()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\n---\n## Heading\n\nBody.\n";
            var path = tempDirectory.CreatePath("lang-csharp.instructions.md");
            File.WriteAllText(path, content);

            // Act
            var read = InstructionsFile.TryParse(path, out var result);

            // Assert
            Assert.Multiple(
                () => Assert.True(read),
                () => Assert.NotNull(result),
                () => Assert.Equal(content, result!.RawContent),
                () => Assert.Equal("lang-csharp (v1.2.3)", result!.Frontmatter.Name));
        }

        [Fact]
        public void Should_return_false_when_the_file_does_not_exist()
        {
            // Arrange
            var path = tempDirectory.CreatePath("absent.instructions.md");

            // Act
            var read = InstructionsFile.TryParse(path, out var result);

            // Assert
            Assert.Multiple(
                () => Assert.False(read),
                () => Assert.Null(result));
        }
    }

    public sealed class TryParseAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_reject_a_null_path()
        {
            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => InstructionsFile.TryParseAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_read_and_parse_an_existing_file()
        {
            // Arrange
            var content = "---\nname: \"lang-csharp (v1.2.3)\"\ndescription: \"C# rules.\"\n---\n## Heading\n\nBody.\n";
            var path = tempDirectory.CreatePath("lang-csharp.instructions.md");
            await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);

            // Act
            var result = await InstructionsFile.TryParseAsync(path, TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Success),
                () => Assert.NotNull(result.Value),
                () => Assert.Equal(content, result.Value!.RawContent),
                () => Assert.Equal("lang-csharp (v1.2.3)", result.Value!.Frontmatter.Name),
                () => Assert.Equal(string.Empty, result.ErrorMessage));
        }

        [Fact]
        public async Task Should_fail_with_an_error_message_when_the_file_does_not_exist()
        {
            // Arrange
            var path = tempDirectory.CreatePath("absent.instructions.md");

            // Act
            var result = await InstructionsFile.TryParseAsync(path, TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.False(result.Success),
                () => Assert.Null(result.Value),
                () => Assert.NotEmpty(result.ErrorMessage));
        }

        [Fact]
        public async Task Should_honour_a_cancelled_token()
        {
            // Arrange
            var content = "---\nname: \"x (v1.0.0)\"\ndescription: \"d\"\n---\nBody.\n";
            var path = tempDirectory.CreatePath("cancel.instructions.md");
            await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            // Act + Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => InstructionsFile.TryParseAsync(path, cancellation.Token));
        }
    }
}
