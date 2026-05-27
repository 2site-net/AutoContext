namespace AutoContext.Engine.Core.Tests.Support.Machine.Housekeeping;

using AutoContext.Engine.Core.Tests.Support.Registry;

internal static class CanonicalCacheLayoutTestSeeder
{
    public static string CreateInstanceSubtree(string cacheRoot, Guid instanceId)
        => CreateInstanceSubtree(cacheRoot, RegistryEntryFakeData.CanonicalWorkspaceHash, instanceId);

    public static string CreateInstanceSubtree(string cacheRoot, string workspaceHash, Guid instanceId)
    {
        var subtree = Path.Combine(cacheRoot, workspaceHash, instanceId.ToString("D"));
        Directory.CreateDirectory(subtree);
        return subtree;
    }
}
