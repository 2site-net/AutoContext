namespace AutoContext.Worker.DotNet.Tests.Support.Tasks.CSharp;

internal static class TestStyleProjectPathsFactory
{
    public static (string ProjectDirectory, string ComparedPath) MakeProjectPaths(params string[] relativeSegments)
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), "AutoContextFakeProj");
        var comparedPath = Path.Combine([projectDirectory, .. relativeSegments]);
        return (projectDirectory, comparedPath);
    }
}
