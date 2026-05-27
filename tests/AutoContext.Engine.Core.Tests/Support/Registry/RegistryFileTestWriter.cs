namespace AutoContext.Engine.Core.Tests.Support.Registry;

using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol.Messages.Registry;

internal static class RegistryFileTestWriter
{
    public static string WriteToCache(string cacheRoot, params RegistryEntry[] entries)
    {
        var path = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
        new RegistryFileWriter(path).Write(entries);
        return path;
    }
}
