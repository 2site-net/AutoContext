namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Machine;

internal static class EngineLogPathTestComposer
{
    public static string Compose(EngineOptions options)
    {
        // Tests pass options constructed via the test fake-data
        // helpers, which always set CacheRootOverride, so we can
        // compose the expected path without depending on
        // EngineCacheRoot.Resolve (internal to engine-core).
        var cacheRoot = options.CacheRootOverride
            ?? throw new InvalidOperationException(
                "Tests using EngineLogPathTestComposer must construct EngineOptions with a non-null CacheRootOverride.");
        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath).Value;

        return Path.Combine(
            cacheRoot,
            workspaceHash,
            options.InstanceId.ToString("D"),
            EngineCrashWriter.LogsSubdirectory,
            LogFileSinkService.EngineLogFileName);
    }
}
