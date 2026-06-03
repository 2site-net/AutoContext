namespace AutoContext.Engine.Core.Tests.Workspace.Context;

using AutoContext.Engine.Core.Workspace.Context;

public sealed class FlagContributionIndexTests
{
    public sealed class ActiveFlags
    {
        [Fact]
        public void Should_be_empty_before_any_contribution()
        {
            // Arrange
            var index = new FlagContributionIndex();

            // Act + Assert
            Assert.Empty(index.ActiveFlags);
        }
    }

    public sealed class Apply
    {
        [Fact]
        public void Should_raise_the_flags_a_path_contributes()
        {
            // Arrange
            var index = new FlagContributionIndex();

            // Act
            index.Apply("a.cs", new HashSet<string> { "hasCSharp" });

            // Assert
            Assert.Contains("hasCSharp", index.ActiveFlags);
        }

        [Fact]
        public void Should_keep_a_flag_active_while_another_path_still_raises_it()
        {
            // Arrange
            var index = new FlagContributionIndex();
            index.Apply("a.cs", new HashSet<string> { "hasCSharp" });
            index.Apply("b.cs", new HashSet<string> { "hasCSharp" });

            // Act
            index.Apply("a.cs", new HashSet<string>());

            // Assert
            Assert.Contains("hasCSharp", index.ActiveFlags);
        }

        [Fact]
        public void Should_drop_a_flag_when_its_last_contributor_is_retracted()
        {
            // Arrange
            var index = new FlagContributionIndex();
            index.Apply("a.cs", new HashSet<string> { "hasCSharp" });
            index.Apply("b.cs", new HashSet<string> { "hasCSharp" });
            index.Apply("a.cs", new HashSet<string>());

            // Act
            index.Apply("b.cs", new HashSet<string>());

            // Assert
            Assert.DoesNotContain("hasCSharp", index.ActiveFlags);
        }

        [Fact]
        public void Should_adjust_flags_when_a_path_is_reclassified()
        {
            // Arrange
            var index = new FlagContributionIndex();
            index.Apply("file", new HashSet<string> { "hasA", "hasB" });

            // Act
            index.Apply("file", new HashSet<string> { "hasB", "hasC" });

            // Assert
            Assert.Multiple(
                () => Assert.DoesNotContain("hasA", index.ActiveFlags),
                () => Assert.Contains("hasB", index.ActiveFlags),
                () => Assert.Contains("hasC", index.ActiveFlags));
        }

        [Fact]
        public void Should_retract_every_flag_for_an_empty_set()
        {
            // Arrange
            var index = new FlagContributionIndex();
            index.Apply("file", new HashSet<string> { "hasA", "hasB" });

            // Act
            index.Apply("file", new HashSet<string>());

            // Assert
            Assert.Empty(index.ActiveFlags);
        }

        [Fact]
        public void Should_count_each_contributor_independently_of_flag_overlap()
        {
            // Arrange
            var index = new FlagContributionIndex();
            index.Apply("a.cs", new HashSet<string> { "hasCSharp", "hasShared" });
            index.Apply("b.fs", new HashSet<string> { "hasFSharp", "hasShared" });

            // Act
            index.Apply("a.cs", new HashSet<string>());

            // Assert
            Assert.Multiple(
                () => Assert.DoesNotContain("hasCSharp", index.ActiveFlags),
                () => Assert.Contains("hasFSharp", index.ActiveFlags),
                () => Assert.Contains("hasShared", index.ActiveFlags));
        }
    }

    public sealed class Clear
    {
        [Fact]
        public void Should_drop_all_active_flags()
        {
            // Arrange
            var index = new FlagContributionIndex();
            index.Apply("a.cs", new HashSet<string> { "hasCSharp" });
            index.Apply("b.fs", new HashSet<string> { "hasFSharp" });

            // Act
            index.Clear();

            // Assert
            Assert.Empty(index.ActiveFlags);
        }

        [Fact]
        public void Should_reset_counts_so_flags_do_not_linger_after_a_rescan()
        {
            // Arrange
            var index = new FlagContributionIndex();
            index.Apply("a.cs", new HashSet<string> { "hasCSharp" });
            index.Apply("b.cs", new HashSet<string> { "hasCSharp" });
            index.Clear();

            // Act
            index.Apply("a.cs", new HashSet<string> { "hasCSharp" });
            index.Apply("a.cs", new HashSet<string>());

            // Assert
            Assert.DoesNotContain("hasCSharp", index.ActiveFlags);
        }
    }
}
