namespace AutoContext.Instructions.Parser.Tests;

public sealed class InstructionsFileTryResultTests
{
    public sealed class Ok
    {
        [Fact]
        public void Should_reject_a_null_value()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFileTryResult.Ok(null!));
        }

        [Fact]
        public void Should_carry_the_value_and_an_empty_error_message()
        {
            // Arrange
            var value = InstructionsFileParser.Parse("## Heading\n\nBody.\n");

            // Act
            var result = InstructionsFileTryResult.Ok(value);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.Success),
                () => Assert.Same(value, result.Value),
                () => Assert.Equal(string.Empty, result.ErrorMessage));
        }
    }

    public sealed class Fail
    {
        [Fact]
        public void Should_reject_a_null_error_message()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => InstructionsFileTryResult.Fail(null!));
        }

        [Fact]
        public void Should_reject_an_empty_error_message()
        {
            // Act + Assert
            Assert.Throws<ArgumentException>(() => InstructionsFileTryResult.Fail(string.Empty));
        }

        [Fact]
        public void Should_carry_the_error_message_and_a_null_value()
        {
            // Act
            var result = InstructionsFileTryResult.Fail("Could not read the file.");

            // Assert
            Assert.Multiple(
                () => Assert.False(result.Success),
                () => Assert.Null(result.Value),
                () => Assert.Equal("Could not read the file.", result.ErrorMessage));
        }
    }
}
