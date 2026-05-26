namespace AutoContext.Engine.Core.Tests.Support.Registry;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;

using Microsoft.Extensions.Logging.Abstractions;

internal static class RegistryEntryReaderTestFactory
{
    public static RegistryEntryReader Create(string path, FakeProcessLookup lookup) =>
        new(
            RegistryFileReaderTestFactory.Create(path),
            lookup,
            NullLogger<RegistryEntryReader>.Instance);
}
