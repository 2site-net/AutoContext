namespace AutoContext.Engine.Core.Tests.Logging.Primitives;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging.Primitives;

public sealed class LogRotationThresholdsTests
{
    [Fact]
    public void ForVerbosity_should_map_normal_to_design_doc_thresholds()
    {
        // Act
        var thresholds = LogRotationThresholds.ForVerbosity(LogVerbosity.Normal);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1_000, thresholds.MaxLines),
            () => Assert.Equal(5L * 1024 * 1024, thresholds.MaxBytes));
    }

    [Fact]
    public void ForVerbosity_should_map_debug_to_design_doc_thresholds()
    {
        // Act
        var thresholds = LogRotationThresholds.ForVerbosity(LogVerbosity.Debug);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(5_000, thresholds.MaxLines),
            () => Assert.Equal(25L * 1024 * 1024, thresholds.MaxBytes));
    }

    [Fact]
    public void ForVerbosity_should_fall_back_to_normal_for_unknown_enum_values()
    {
        // Act — forward-compatibility guard: undefined enum
        // values resolve to the same thresholds as Normal.
        var unknown = (LogVerbosity)int.MaxValue;
        var thresholds = LogRotationThresholds.ForVerbosity(unknown);

        // Assert
        Assert.Equal(
            LogRotationThresholds.ForVerbosity(LogVerbosity.Normal),
            thresholds);
    }
}
