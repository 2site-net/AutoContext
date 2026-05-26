namespace AutoContext.Engine.Core.Tests.Support.Logging.Primitives;

using AutoContext.Engine.Core.Logging.Primitives;

internal static class LogRotationThresholdsFakeData
{
    public static LogRotationThresholds Normal { get; } =
        LogRotationThresholds.ForVerbosity(LogVerbosity.Normal);
}
