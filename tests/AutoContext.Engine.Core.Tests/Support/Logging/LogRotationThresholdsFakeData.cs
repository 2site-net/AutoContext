namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Logging;

internal static class LogRotationThresholdsFakeData
{
    public static LogRotationThresholds Normal { get; } =
        LogRotationThresholds.ForVerbosity(LogVerbosity.Normal);
}
