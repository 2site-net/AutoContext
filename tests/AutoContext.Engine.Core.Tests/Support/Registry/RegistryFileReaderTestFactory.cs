namespace AutoContext.Engine.Core.Tests.Support.Registry;

using AutoContext.Engine.Core.Registry;

/// <summary>
/// Builds a <see cref="RegistryFileReader"/> wired with the short
/// retry windows used by the registry tests so they don't pay the
/// production-default backoff cost.
/// </summary>
internal static class RegistryFileReaderTestFactory
{
    public static RegistryFileReader Create(string path) =>
        new(
            path,
            new RegistryFileReaderOptions
            {
                InitialRetryDelay = TimeSpan.FromMilliseconds(1),
                MaxRetryDelay = TimeSpan.FromMilliseconds(5),
                MaxAttempts = 5,
            });
}
