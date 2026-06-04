namespace AutoContext.Engine.Tests.Support.IO;

public static class WorkspaceTestDirectoryFactory
{
    public static string Create()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "autocontext-engine-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
