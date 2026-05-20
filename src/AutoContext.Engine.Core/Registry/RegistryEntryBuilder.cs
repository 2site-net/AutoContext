namespace AutoContext.Engine.Core.Registry;

using System.Diagnostics;
using System.Reflection;

using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// Pure builder for the <see cref="RegistryEntry"/> value that
/// represents <i>this</i> engine instance in the shared liveness
/// registry (<c>engine-registry.json</c>). Stateless and
/// side-effect-free: composes <see cref="EngineOptions"/>
/// (workspace path, instance id, instance label, retention
/// window) with runtime facts — the workspace hash computed via
/// <see cref="WorkspaceHash.Compute"/>, the current process id
/// and start time (used by housekeeping to defeat pid recycling),
/// the assembly's informational version, and a
/// <see cref="TimeProvider"/>-driven <c>StartedAt</c> stamp.
/// </summary>
/// <remarks>
/// The builder owns <i>construction</i> only. Writing the entry
/// to disk, and removing it on graceful shutdown, lives in
/// <see cref="RegistryFileService"/>; the two collaborate via DI
/// so the construction concern stays trivially unit-testable and
/// the file service stays free of <see cref="EngineOptions"/> /
/// <see cref="TimeProvider"/> ceremony at its public seam.
/// </remarks>
internal static class RegistryEntryBuilder
{
    /// <summary>
    /// Builds the registry entry that represents this engine
    /// instance. Pure: no IO, no side effects, no DI graph
    /// inspection.
    /// </summary>
    /// <param name="options">Engine options resolved from the
    /// host's options pipeline. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="clock">Clock source for the <c>startedAt</c>
    /// stamp. Pass <see cref="TimeProvider.System"/> in production
    /// and a controlled clock in tests. Must not be
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Any required
    /// parameter is <see langword="null"/>.</exception>
    public static RegistryEntry Build(EngineOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        using var process = Process.GetCurrentProcess();
        var hash = WorkspaceHash.Compute(options.WorkspacePath);

        return new RegistryEntry(
            EngineVersion: ResolveEngineVersion(),
            WorkspaceHash: hash.Value,
            WorkspacePath: Path.GetFullPath(options.WorkspacePath),
            InstanceId: options.InstanceId,
            InstanceLabel: options.InstanceLabel,
            ProcessId: process.Id,
            ProcessStartTimeUtc: process.StartTime.ToUniversalTime(),
            StartedAt: clock.GetUtcNow(),
            Retention: options.Retention);
    }

    private static string ResolveEngineVersion()
    {
        var assembly = typeof(RegistryEntryBuilder).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
