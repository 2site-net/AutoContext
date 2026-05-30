namespace AutoContext.Engine.Core.Tests.Support.Registry;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// Builds a <see cref="RegistryFileService"/> wired with the short
/// retry windows used by the registry tests so they don't pay the
/// production-default backoff cost.
/// </summary>
internal static class RegistryFileServiceTestFactory
{
    public static RegistryFileService CreateService(
        string path,
        RegistryFileServiceOptions? options = null,
        Func<JsonRegistryEntry>? ownEntryFactory = null) =>
        new(
            path,
            options,
            new RegistryFileReaderOptions
            {
                InitialRetryDelay = TimeSpan.FromMilliseconds(1),
                MaxRetryDelay = TimeSpan.FromMilliseconds(5),
                MaxAttempts = 5,
            },
            loggerFactory: null,
            ownEntryFactory: ownEntryFactory);
}
