namespace AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// Allocates a per-test workspace directory under the OS temp folder,
/// wrapped in a <see cref="TempDirectory"/> handle that deletes it on
/// dispose.
/// </summary>
public static class WorkspaceTestDirectoryFactory
{
    public static TempDirectory Create() =>
        TempDirectory.CreateNew("autocontext-engine-tests");
}
