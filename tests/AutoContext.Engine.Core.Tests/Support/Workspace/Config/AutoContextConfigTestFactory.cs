namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;

/// <summary>
/// Builds an <see cref="AutoContextConfigManager"/> bound to a test
/// workspace directory with a fixed engine version so on-disk
/// <c>version</c> stamps are deterministic.
/// </summary>
internal static class AutoContextConfigTestFactory
{
    public const string EngineVersion = "9.9.9";

    public static AutoContextConfigManager Create(
        string workspacePath,
        string? engineVersion = null,
        TimeProvider? timeProvider = null,
        TimeSpan? batchWindow = null)
        => new(
            workspacePath,
            engineVersion ?? EngineVersion,
            timeProvider: timeProvider,
            batchWindow: batchWindow);
}
