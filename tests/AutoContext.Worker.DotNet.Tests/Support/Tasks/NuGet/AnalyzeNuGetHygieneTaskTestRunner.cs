namespace AutoContext.Worker.DotNet.Tests.Support.Tasks.NuGet;

using AutoContext.Framework.Tests.Support.Workers;
using AutoContext.Worker.DotNet.Tasks.NuGet;

internal static class AnalyzeNuGetHygieneTaskTestRunner
{
    public static async Task<(bool Passed, string Report)> RunAsync(string content)
    {
        var sut = new AnalyzeNuGetHygieneTask();
        var output = await sut.ExecuteAsync(new { content }).ConfigureAwait(false);

        var passed = output.GetProperty("passed").GetBoolean();
        var report = output.GetProperty("report").GetString()!;

        return (passed, report);
    }
}
