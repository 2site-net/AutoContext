namespace AutoContext.Engine.Tests.Integration;

using System.Globalization;
using System.IO;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Pipes;

using Xunit.Sdk;

/// <summary>
/// Integration coverage for the cross-engine housekeeping sweep:
/// when one engine is hard-killed (no graceful shutdown) a surviving
/// peer sharing the same workspace and cache root must reap the dead
/// instance's cache subtree on its own shutdown, while preserving its
/// own subtree and the shared cache root.
/// </summary>
/// <remarks>
/// Spawns two <c>autocontext-engine</c> instances against one
/// workspace and a shared <c>--cache-root</c>, hard-kills one, then
/// shuts the survivor down gracefully and asserts the sweep outcome.
/// Gated with the repository's <c>Category=Smoke</c> trait so it runs
/// under <c>.\build.ps1 Compile -Smoke DotNet</c> and stays out of
/// the default unit-test pass.
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class CrossInstanceCacheSweepTests
{
    [Fact]
    public async Task Should_sweep_hard_killed_peer_cache_subtree_on_survivor_shutdown()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();
        var workspacePath = workspace.Path;
        var killedInstanceId = Guid.NewGuid();
        var survivorInstanceId = Guid.NewGuid();
        var workspaceHash = WorkspaceHash.Compute(workspacePath).Value;
        var killedSubtreePath = Path.Combine(cache.Path, workspaceHash, killedInstanceId.ToString("D"));
        var survivorSubtreePath = Path.Combine(cache.Path, workspaceHash, survivorInstanceId.ToString("D"));

        await using var killed = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspacePath,
                InstanceId = killedInstanceId,
                CacheRootOverride = cache.Path,
                Retention = "1s",
            },
        };
        await killed.SpawnAsync(ct);

        await using var survivor = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspacePath,
                InstanceId = survivorInstanceId,
                CacheRootOverride = cache.Path,
                Retention = "1s",
            },
        };
        await survivor.SpawnAsync(ct);

        await WaitForSubtreeAsync(killedSubtreePath);
        await WaitForSubtreeAsync(survivorSubtreePath);
        var killedSubtreeExistedBeforeKill = Directory.Exists(killedSubtreePath);
        var survivorSubtreeExistedBeforeKill = Directory.Exists(survivorSubtreePath);

        // Act
        killed.Process.Kill(entireProcessTree: true);
        await killed.Process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        try
        {
            await EngineWireTestClient.ShutdownGracefullyAsync(survivor, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new XunitException(BuildDiagnostic("Survivor graceful shutdown failed."), ex);
        }

        // Assert
        Assert.Multiple(
            () => Assert.True(killedSubtreeExistedBeforeKill,
                $"Expected the killed engine's subtree '{killedSubtreePath}' to exist before the hard-kill."),
            () => Assert.True(survivorSubtreeExistedBeforeKill,
                $"Expected the survivor engine's subtree '{survivorSubtreePath}' to exist before the hard-kill."),
            () => Assert.False(Directory.Exists(killedSubtreePath), BuildDiagnostic(
                $"Expected the survivor's shutdown sweep to reap the killed engine's subtree '{killedSubtreePath}'.")),
            () => Assert.True(Directory.Exists(survivorSubtreePath), BuildDiagnostic(
                $"Expected the survivor's own subtree '{survivorSubtreePath}' to be preserved (Registered at sweep time).")),
            () => Assert.True(Directory.Exists(cache.Path),
                $"Expected the shared cache root '{cache.Path}' to be preserved."),
            () => Assert.Equal(0, survivor.Process.ExitCode));

        async Task WaitForSubtreeAsync(string path)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (!Directory.Exists(path))
            {
                ct.ThrowIfCancellationRequested();
                if (DateTimeOffset.UtcNow > deadline)
                {
                    throw new TimeoutException($"Engine subtree '{path}' did not materialise within 15s.");
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        string BuildDiagnostic(string headline)
        {
            return string.Join(
                Environment.NewLine,
                headline,
                $"CacheRoot: {cache.Path} (exists={Directory.Exists(cache.Path)})",
                $"KilledSubtree: {killedSubtreePath} (exists={Directory.Exists(killedSubtreePath)})",
                $"SurvivorSubtree: {survivorSubtreePath} (exists={Directory.Exists(survivorSubtreePath)})",
                $"Killed engine: HasExited={killed.Process.HasExited}, ExitCode={(killed.Process.HasExited ? killed.Process.ExitCode.ToString(CultureInfo.InvariantCulture) : "<running>")}",
                $"Survivor engine: HasExited={survivor.Process.HasExited}, ExitCode={(survivor.Process.HasExited ? survivor.Process.ExitCode.ToString(CultureInfo.InvariantCulture) : "<running>")}",
                "Killed stderr:",
                killed.StandardErrorLines.Count == 0 ? "(none)" : string.Join(Environment.NewLine, killed.StandardErrorLines),
                "Survivor stderr:",
                survivor.StandardErrorLines.Count == 0 ? "(none)" : string.Join(Environment.NewLine, survivor.StandardErrorLines));
        }
    }
}
