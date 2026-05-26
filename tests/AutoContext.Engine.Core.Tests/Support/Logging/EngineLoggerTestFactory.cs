namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Shared;

internal static class EngineLoggerTestFactory
{
    public static EngineLoggerContext Create(DateTimeOffset now) =>
        Create(now, category: "engine.test");

    public static EngineLoggerContext Create(DateTimeOffset now, string category)
    {
        var channel = new LogChannel();
        var logger = new EngineLogger(category, channel, new FakeTimeProvider(now));
        return new EngineLoggerContext(logger, channel);
    }

    internal sealed record EngineLoggerContext(EngineLogger Logger, LogChannel Channel);
}
