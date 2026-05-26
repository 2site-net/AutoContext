namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Logging.Primitives;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class RotatedLogCleanerTestFactory
{
    public static RotatedLogCleaner Create(EngineOptions options)
        => Create(options, TimeProvider.System);

    public static RotatedLogCleaner Create(EngineOptions options, TimeProvider clock)
        => new(
            new RetentionPolicy(Options.Create(options), clock),
            NullLogger<RotatedLogCleaner>.Instance);
}
