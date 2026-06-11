namespace AutoContext.Instructions.Parser.Tests.Model;

using AutoContext.Instructions.Parser.Model;
using AutoContext.Instructions.Parser.Tests.Support;

public sealed class InstructionsFileBodyTests
{
    public sealed class WithoutTaggedRules
    {
        [Fact]
        public void Should_reject_null_rule_ids()
        {
            // Arrange
            var body = InstructionsFileSpanStream.Parse("## Rules\n\n- [INST0001] **Do** first\n").Body;

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => body.WithoutTaggedRules(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Should_return_the_same_instance_when_the_set_is_empty()
        {
            // Arrange
            var body = InstructionsFileSpanStream.Parse("## Rules\n\n- [INST0001] **Do** first\n").Body;

            // Act
            var result = body.WithoutTaggedRules(RuleIds(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(body, result);
        }

        [Fact]
        public void Should_return_the_same_instance_when_no_rule_matches()
        {
            // Arrange
            var body = InstructionsFileSpanStream.Parse("## Rules\n\n- [INST0001] **Do** first\n").Body;

            // Act
            var result = body.WithoutTaggedRules(RuleIds("INST9999"), TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(body, result);
        }

        [Fact]
        public void Should_remove_the_named_rule()
        {
            // Arrange
            var content = "## Rules\n\n- [INST0001] **Do** first\n- [INST0002] **Don't** second\n";
            var body = InstructionsFileSpanStream.Parse(content).Body;

            // Act
            var result = body.WithoutTaggedRules(RuleIds("INST0001"), TestContext.Current.CancellationToken);
            var rule = Assert.Single(result.Rules);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("INST0002", rule.Id),
                () => Assert.DoesNotContain("INST0001", result.RawValue, StringComparison.Ordinal),
                () => Assert.Equal(
                    "## Rules\n\n- [INST0002] **Don't** second\n",
                    result.RawValue));
        }

        [Fact]
        public void Should_remove_every_line_a_multi_line_rule_covers()
        {
            // Arrange
            var content =
                "## Rules\n\n- [INST0001] **Do** first\n  continued detail\n- [INST0002] **Do** second\n";
            var body = InstructionsFileSpanStream.Parse(content).Body;

            // Act
            var result = body.WithoutTaggedRules(RuleIds("INST0001"), TestContext.Current.CancellationToken);
            var rule = Assert.Single(result.Rules);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("INST0002", rule.Id),
                () => Assert.DoesNotContain("continued detail", result.RawValue, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_keep_rules_without_a_tag()
        {
            // Arrange
            var content = "## Rules\n\n- **Do** keep me\n- [INST0001] **Do** drop me\n";
            var body = InstructionsFileSpanStream.Parse(content).Body;

            // Act
            var result = body.WithoutTaggedRules(RuleIds("INST0001"), TestContext.Current.CancellationToken);
            var rule = Assert.Single(result.Rules);

            // Assert
            Assert.Multiple(
                () => Assert.Null(rule.Id),
                () => Assert.Contains("keep me", result.RawValue, StringComparison.Ordinal),
                () => Assert.DoesNotContain("INST0001", result.RawValue, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_reanchor_sections_against_the_shortened_text()
        {
            // Arrange
            var content =
                "## Alpha\n\n- [INST0001] **Do** alpha\n\n## Beta\n\n- [INST0002] **Do** beta\n";
            var body = InstructionsFileSpanStream.Parse(content).Body;

            // Act
            var result = body.WithoutTaggedRules(RuleIds("INST0001"), TestContext.Current.CancellationToken);
            var beta = result.Sections.Single(section => section.Heading == "Beta");

            // Assert — the surviving section's offset indexes the new text, not the old.
            Assert.Multiple(
                () => Assert.Equal(["Alpha", "Beta"], result.Sections.Select(section => section.Heading)),
                () => Assert.StartsWith(
                    "## Beta",
                    result.RawValue[beta.TextSpan.StartIndex..],
                    StringComparison.Ordinal));
        }

        private static HashSet<string> RuleIds(params string[] ids)
            => new(ids, StringComparer.Ordinal);
    }
}
