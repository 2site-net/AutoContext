namespace AutoContext.Engine.Core.Tests.Support;

using AutoContext.Engine.Core;

internal static class EngineOptionsFakeData
{
    private static readonly Guid InstanceId =
        Guid.Parse("11111111-2222-4333-8444-555555555555");

    private static readonly string WorkspacePath =
        OperatingSystem.IsWindows() ? @"C:\workspace" : "/workspace";

    public static EngineOptions CreateValidOptions() =>
        new()
        {
            WorkspacePath = WorkspacePath,
            InstanceId = InstanceId,
        };

    public static Guid GetInstanceId() => InstanceId;

    public static string GetWorkspacePath() => WorkspacePath;
}
