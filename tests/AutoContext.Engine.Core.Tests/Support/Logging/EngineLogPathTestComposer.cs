namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Tests.Support.Machine;

internal static class EngineLogPathTestComposer
{
    public static string Compose(EngineOptions options) =>
        EngineCacheLayoutTestFactory.Create(options).EngineLogFilePath;
}
