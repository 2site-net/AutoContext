namespace AutoContext.Engine.Core.Tests.Registry;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Fakes;
using AutoContext.Engine.Core.Tests.Testing.Utils;

public sealed class RegistryEntryBuilderTests
{
    public sealed class Build
    {
        [Fact]
        public void Should_reject_null_arguments()
        {
            // Arrange
            var options = EngineOptionsFakeData.CreateValidOptions();

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(() =>
                    RegistryEntryBuilder.Build(null!, TimeProvider.System)),
                () => Assert.Throws<ArgumentNullException>(() =>
                    RegistryEntryBuilder.Build(options, null!)));
        }

        [Fact]
        public void Should_compose_options_and_runtime_facts_with_supplied_clock()
        {
            // Arrange
            var options = EngineOptionsFakeData.CreateValidOptions();
            options.InstanceLabel = "label-A";
            options.Retention = TimeSpan.FromHours(7);
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

            // Act
            var entry = RegistryEntryBuilder.Build(options, clock);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(options.InstanceId, entry.InstanceId),
                () => Assert.Equal(options.InstanceLabel, entry.InstanceLabel),
                () => Assert.Equal(options.Retention, entry.Retention),
                () => Assert.Equal(Path.GetFullPath(options.WorkspacePath), entry.WorkspacePath),
                () => Assert.Equal(
                    WorkspaceHash.Compute(options.WorkspacePath).Value,
                    entry.WorkspaceHash),
                () => Assert.Equal(Environment.ProcessId, entry.ProcessId),
                () => Assert.Equal(clock.GetUtcNow(), entry.StartedAt),
                () => Assert.False(string.IsNullOrWhiteSpace(entry.EngineVersion)),
                () => Assert.Equal(DateTimeKind.Utc, entry.ProcessStartTimeUtc.UtcDateTime.Kind));
        }
    }
}
