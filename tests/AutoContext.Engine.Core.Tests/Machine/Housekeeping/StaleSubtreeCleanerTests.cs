namespace AutoContext.Engine.Core.Tests.Machine.Housekeeping;

using AutoContext.Engine.Core.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Logging.Primitives;
using AutoContext.Engine.Core.Tests.Support.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Shared;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class StaleSubtreeCleanerTests
{
    private static readonly DateTimeOffset KnownNow =
        new(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_invalid_arguments()
        {
            // Arrange
            var policy = RetentionPolicyTestFactory.Create(TimeSpan.FromMinutes(10), KnownNow);

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => new StaleSubtreeCleaner(null!, NullLogger<StaleSubtreeCleaner>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new StaleSubtreeCleaner(policy, null!)));
        }
    }

    public sealed class Sweep(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_throw_when_classifications_is_null()
        {
            // Arrange
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.FromMinutes(10), KnownNow);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => sut.Sweep(classifications: null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Should_skip_Registered_subtree()
        {
            // Arrange
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.Zero, KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                StartedAt = KnownNow - TimeSpan.FromDays(30),
                Retention = TimeSpan.FromHours(1),
            };
            var status = new SubtreeRegistryStatus.Registered(subtree, entry);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, deleted),
                () => Assert.True(Directory.Exists(subtree)));
        }

        [Fact]
        public void Should_delete_StaleRegistration_when_entry_retention_elapsed()
        {
            // Arrange — entry started 2h ago with 1h retention => expired.
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.FromDays(7), KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                StartedAt = KnownNow - TimeSpan.FromHours(2),
                Retention = TimeSpan.FromHours(1),
            };
            var status = new SubtreeRegistryStatus.StaleRegistration(subtree, entry);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, deleted),
                () => Assert.False(Directory.Exists(subtree)));
        }

        [Fact]
        public void Should_preserve_StaleRegistration_while_inside_entry_retention()
        {
            // Arrange — entry started 30min ago with 1h retention.
            // Engine's --retention is Zero (would expire if consulted) — the
            // entry's own retention wins.
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.Zero, KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                StartedAt = KnownNow - TimeSpan.FromMinutes(30),
                Retention = TimeSpan.FromHours(1),
            };
            var status = new SubtreeRegistryStatus.StaleRegistration(subtree, entry);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, deleted),
                () => Assert.True(Directory.Exists(subtree)));
        }

        [Fact]
        public void Should_delete_Unregistered_subtree_when_engine_retention_elapsed()
        {
            // Arrange — engine retention is Zero, so any subtree
            // older than "now" expires immediately.
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.Zero, KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            var status = new SubtreeRegistryStatus.Unregistered(subtree);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, deleted),
                () => Assert.False(Directory.Exists(subtree)));
        }

        [Fact]
        public void Should_preserve_Unregistered_subtree_while_inside_engine_retention()
        {
            // Arrange — engine retention is 1 day; subtree was just
            // created so creation time is "now-ish".
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.FromDays(1), KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            var status = new SubtreeRegistryStatus.Unregistered(subtree);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, deleted),
                () => Assert.True(Directory.Exists(subtree)));
        }

        [Fact]
        public void Should_delete_Foreign_subtree_when_engine_retention_elapsed()
        {
            // Arrange
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.Zero, KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            var status = new SubtreeRegistryStatus.Foreign(subtree);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, deleted),
                () => Assert.False(Directory.Exists(subtree)));
        }

        [Fact]
        public void Should_preserve_Foreign_subtree_while_inside_engine_retention()
        {
            // Arrange
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.FromDays(1), KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            var status = new SubtreeRegistryStatus.Foreign(subtree);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, deleted),
                () => Assert.True(Directory.Exists(subtree)));
        }

        [Fact]
        public void Should_treat_DirectoryNotFoundException_mid_delete_as_success()
        {
            // Arrange — StaleRegistration arm bypasses the
            // TryGetCreationTime probe (it uses entry timestamps),
            // so the Delete call hits DirectoryNotFoundException
            // directly when a peer has already reaped the subtree.
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.FromDays(7), KnownNow);
            var ghost = Path.Combine(tempDirectory.CreateDirectory(), "vanished");
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                StartedAt = KnownNow - TimeSpan.FromHours(2),
                Retention = TimeSpan.FromHours(1),
            };
            var status = new SubtreeRegistryStatus.StaleRegistration(ghost, entry);

            // Act
            var deleted = sut.Sweep([status], TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, deleted);
        }

        [Fact]
        public void Should_continue_after_per_subtree_failure()
        {
            // Arrange — first status points at a ghost path
            // (StaleRegistration → succeeds via DNF race), second
            // is a real subtree that must still be reaped.
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.FromDays(7), KnownNow);
            var ghost = Path.Combine(tempDirectory.CreateDirectory(), "vanished");
            var real = tempDirectory.CreateDirectory();
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                StartedAt = KnownNow - TimeSpan.FromHours(2),
                Retention = TimeSpan.FromHours(1),
            };

            // Act
            var deleted = sut.Sweep(
            [
                new SubtreeRegistryStatus.StaleRegistration(ghost, entry),
                new SubtreeRegistryStatus.StaleRegistration(real, entry),
            ],
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, deleted),
                () => Assert.False(Directory.Exists(real)));
        }

        [Fact]
        public void Should_propagate_cancellation_between_entries()
        {
            // Arrange
            var sut = StaleSubtreeCleanerTestFactory.Create(TimeSpan.Zero, KnownNow);
            var subtree = tempDirectory.CreateDirectory();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act + Assert
            Assert.Throws<OperationCanceledException>(
                () => sut.Sweep(
                    [new SubtreeRegistryStatus.Unregistered(subtree)],
                    cts.Token));
        }
    }
}
