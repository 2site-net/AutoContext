namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Logging;

internal static class RotatedLogFileTestComposer
{
    public static string Seed(string directory, string baseName, DateTimeOffset stamp)
    {
        var path = Path.Combine(directory, RotatedLogCleaner.ComposeRotatedFileName(baseName, stamp));
        File.WriteAllText(path, "{}\n");
        return path;
    }
}
