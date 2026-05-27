namespace AutoContext.Engine.Core.Tests.Support.Registry;

using AutoContext.Engine.Protocol.Messages.Registry;

internal static class RegistryEntryFakeData
{
    public const string CanonicalWorkspaceHash = "0123456789ABCDEF";

    private static readonly string WorkspacePath =
        OperatingSystem.IsWindows() ? @"C:\workspaces\test" : "/workspaces/test";

    public static RegistryEntry CreateValidEntry() =>
        new(
            EngineVersion: "0.9.5",
            WorkspaceHash: CanonicalWorkspaceHash,
            WorkspacePath: WorkspacePath,
            InstanceId: Guid.NewGuid(),
            InstanceLabel: "test",
            ProcessId: Environment.ProcessId,
            ProcessStartTimeUtc: DateTimeOffset.UtcNow,
            StartedAt: DateTimeOffset.UtcNow,
            Retention: TimeSpan.FromHours(12));
}
