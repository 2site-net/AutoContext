namespace AutoContext.Engine.Core.Tests.Support.Workspace.Context;

public static class WorkspaceFileTestWriter
{
    public static void Write(string workspaceRoot, string relativePath, string contents = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, contents);
    }
}
