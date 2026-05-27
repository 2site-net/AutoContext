namespace AutoContext.Engine.Core.Tests.Support.Machine;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Machine;

using Microsoft.Extensions.Options;

/// <summary>
/// Shared helper for tests that need an <see cref="EngineCacheLayout"/>
/// composed from <see cref="EngineOptions"/>. Production composes the
/// layout via DI off the <see cref="CacheRoot"/> singleton; tests
/// skip the container and instantiate the same two types directly
/// so fixtures stay cheap.
/// </summary>
internal static class EngineCacheLayoutTestFactory
{
    public static EngineCacheLayout Create(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new EngineCacheLayout(new CacheRoot(Options.Create(options)));
    }
}
