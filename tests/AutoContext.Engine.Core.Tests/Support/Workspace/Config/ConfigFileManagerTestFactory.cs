namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds an <see cref="ConfigFileManager"/> bound to a test
/// workspace directory with a fixed engine version so on-disk
/// <c>version</c> stamps are deterministic.
/// </summary>
internal static class ConfigFileManagerTestFactory
{
    public const string EngineVersion = "9.9.9";

    public static ConfigFileManager Create(
        string workspacePath,
        string? engineVersion = null,
        TimeProvider? timeProvider = null,
        TimeSpan? batchWindow = null)
        => new(
            workspacePath,
            engineVersion ?? EngineVersion,
            timeProvider ?? TimeProvider.System,
            ConfigFileManager.DefaultDebounceDelay,
            batchWindow ?? ConfigFileManager.DefaultBatchWindow,
            NullLogger<ConfigFileManager>.Instance);
}
