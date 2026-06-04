namespace AutoContext.Engine.Core.Tests.Support.Registry;

using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol.Messages.Registry;

using Microsoft.Extensions.Logging.Abstractions;

internal static class RegistryFileTestWriter
{
    public static string WriteToCache(string cacheRoot, params JsonRegistryEntry[] entries)
    {
        var path = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
        Write(path, entries);
        return path;
    }

    public static void Write(string path, params JsonRegistryEntry[] entries)
        => new RegistryFileWriter(path, NullLogger<RegistryFileWriter>.Instance).Write(entries);
}
