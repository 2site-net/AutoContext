namespace AutoContext.Engine.Core.Tests.Testing.Utils;

using AutoContext.Engine.Protocol.Messages.Registry;

internal static class RegistryEntryFakeData
{
    private static readonly string WorkspacePath =
        OperatingSystem.IsWindows() ? @"C:\workspaces\test" : "/workspaces/test";

    public static RegistryEntry CreateValidEntry() =>
        new(
            EngineVersion: "0.9.5",
            WorkspaceHash: "0123456789ABCDEF",
            WorkspacePath: WorkspacePath,
            InstanceId: Guid.NewGuid(),
            InstanceLabel: "test",
            ProcessId: Environment.ProcessId,
            ProcessStartTimeUtc: DateTimeOffset.UtcNow,
            StartedAt: DateTimeOffset.UtcNow,
            Retention: TimeSpan.FromHours(12));
}
