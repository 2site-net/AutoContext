namespace AutoContext.Engine.Core.Tests.Support.Machine.Housekeeping;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class CacheRootScannerTestFactory
{
    public static CacheRootScanner Create(string cacheRootPath, string registryFilePath, FakeProcessLookup lookup)
    {
        var options = new EngineOptions
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
            CacheRootOverride = cacheRootPath,
        };

        return new CacheRootScanner(
            new CacheRoot(Options.Create(options)),
            RegistryEntryReaderTestFactory.Create(registryFilePath, lookup),
            NullLogger<CacheRootScanner>.Instance);
    }
}
