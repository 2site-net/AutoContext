namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;

public sealed class LogRotationThresholdsTests
{
    [Fact]
    public void ForRotationSize_should_map_small_to_design_doc_thresholds()
    {
        // Act
        var thresholds = LogRotationThresholds.ForRotationSize(LogRotationSize.Small);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1_000, thresholds.MaxLines),
            () => Assert.Equal(5L * 1024 * 1024, thresholds.MaxBytes));
    }

    [Fact]
    public void ForRotationSize_should_map_large_to_design_doc_thresholds()
    {
        // Act
        var thresholds = LogRotationThresholds.ForRotationSize(LogRotationSize.Large);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(5_000, thresholds.MaxLines),
            () => Assert.Equal(25L * 1024 * 1024, thresholds.MaxBytes));
    }

    [Fact]
    public void ForRotationSize_should_fall_back_to_small_for_unknown_enum_values()
    {
        // Act — forward-compatibility guard: undefined enum
        // values resolve to the same thresholds as Small.
        var unknown = (LogRotationSize)int.MaxValue;
        var thresholds = LogRotationThresholds.ForRotationSize(unknown);

        // Assert
        Assert.Equal(
            LogRotationThresholds.ForRotationSize(LogRotationSize.Small),
            thresholds);
    }
}
