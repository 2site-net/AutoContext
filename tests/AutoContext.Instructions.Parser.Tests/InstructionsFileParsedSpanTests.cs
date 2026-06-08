namespace AutoContext.Instructions.Parser.Tests;

public sealed class InstructionsFileParsedSpanTests
{
    public sealed class Equality
    {
        [Fact]
        public void Should_treat_identical_content_from_different_buffers_as_equal()
        {
            // Arrange
            var left = new InstructionsFileParsedSpan(
                "## Heading\n".AsMemory(),
                InstructionsFileSpanKind.Heading2,
                new InstructionsFileTextSpan(0, 11),
                new InstructionsFileLineSpan(0, 1));
            var right = new InstructionsFileParsedSpan(
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
            var left = new InstructionsFileParsedSpan(
                "hello".AsMemory(),
                InstructionsFileSpanKind.Text,
                new InstructionsFileTextSpan(0, 5),
                new InstructionsFileLineSpan(0, 1));
            var right = left with { Text = "world".AsMemory() };

            // Act + Assert
            Assert.NotEqual(left, right);
        }

        [Fact]
        public void Should_treat_equal_diagnostics_from_distinct_lists_as_equal()
        {
            // Arrange
            var left = new InstructionsFileParsedSpan(
                "[INST0001]".AsMemory(),
                InstructionsFileSpanKind.TaggedRule,
                new InstructionsFileTextSpan(0, 10),
                new InstructionsFileLineSpan(0, 1))
            {
                Diagnostics = [new InstructionsFileDiagnostic(InstructionsFileDiagnosticKind.DuplicateTag, "duplicate")],
            };
            var right = left with
            {
                Diagnostics = [new InstructionsFileDiagnostic(InstructionsFileDiagnosticKind.DuplicateTag, "duplicate")],
            };

            // Act + Assert
            Assert.Multiple(
                () => Assert.Equal(left, right),
                () => Assert.Equal(left.GetHashCode(), right.GetHashCode()));
        }

        [Fact]
        public void Should_distinguish_spans_with_different_diagnostics()
        {
            // Arrange
            var span = new InstructionsFileParsedSpan(
                "[INST0001]".AsMemory(),
                InstructionsFileSpanKind.TaggedRule,
                new InstructionsFileTextSpan(0, 10),
                new InstructionsFileLineSpan(0, 1));
            var flagged = span with
            {
                Diagnostics = [new InstructionsFileDiagnostic(InstructionsFileDiagnosticKind.DuplicateTag, "duplicate")],
            };

            // Act + Assert
            Assert.NotEqual(span, flagged);
        }
    }
}
