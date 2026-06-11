namespace AutoContext.Instructions.Parser.Tests.Syntax;

using AutoContext.Instructions.Parser.Syntax;

public sealed class InstructionsFileSyntaxSpanTests
{
    public sealed class Equality
    {
        [Fact]
        public void Should_treat_identical_content_from_different_buffers_as_equal()
        {
            // Arrange
            var left = new InstructionsFileSyntaxSpan(
                "## Heading\n".AsMemory(),
                InstructionsFileSpanKind.Heading2,
                new InstructionsFileTextSpan(0, 11),
                new InstructionsFileLineSpan(0, 1));
            var right = new InstructionsFileSyntaxSpan(
                "prefix ## Heading\n".AsMemory(7, 11),
                InstructionsFileSpanKind.Heading2,
                new InstructionsFileTextSpan(0, 11),
                new InstructionsFileLineSpan(0, 1));

            // Act + Assert
            Assert.Multiple(
                () => Assert.Equal(left, right),
                () => Assert.True(left == right),
                () => Assert.Equal(left.GetHashCode(), right.GetHashCode()));
        }

        [Fact]
        public void Should_distinguish_spans_with_different_text_content()
        {
            // Arrange
            var left = new InstructionsFileSyntaxSpan(
                "hello".AsMemory(),
                InstructionsFileSpanKind.Text,
                new InstructionsFileTextSpan(0, 5),
                new InstructionsFileLineSpan(0, 1));
            var right = left with { Text = "world".AsMemory() };

            // Act + Assert
            Assert.NotEqual(left, right);
        }
    }
}
