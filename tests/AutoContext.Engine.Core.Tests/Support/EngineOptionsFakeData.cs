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

    /// <summary>
    /// Returns an <see cref="Action{EngineOptions}"/> delegate that
    /// populates the workspace, instance id, and the supplied cache-root
    /// override on a configurable options instance. Used as the configure
    /// callback for host-builder tests.
    /// </summary>
    public static Action<EngineOptions> ConfigureValidWith(string cacheRootOverride) =>
        options =>
        {
            ArgumentNullException.ThrowIfNull(options);
            options.WorkspacePath = WorkspacePath;
            options.InstanceId = InstanceId;
            options.CacheRootOverride = cacheRootOverride;
        };
}
