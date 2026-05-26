namespace AutoContext.Engine.Core.Tests.Support.Housekeeping;

using AutoContext.Engine.Core.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;

using Microsoft.Extensions.Logging.Abstractions;

internal static class CacheRootScannerTestFactory
{
    public static CacheRootScanner Create(string cacheRootPath, string registryFilePath, FakeProcessLookup lookup) =>
        new(
            cacheRootPath,
            RegistryEntryReaderTestFactory.Create(registryFilePath, lookup),
            NullLogger<CacheRootScanner>.Instance);
}
