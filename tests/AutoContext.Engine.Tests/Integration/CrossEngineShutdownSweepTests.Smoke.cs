namespace AutoContext.Engine.Tests.Integration;

using System.Globalization;
using System.IO;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Tests.Support.Integration;

using Xunit.Sdk;

[Trait("Category", "Smoke")]
public sealed class CrossEngineShutdownSweepTests
{
    [Fact]
    public async Task Survivor_shutdown_sweep_should_reap_hard_killed_peer_subtree()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var cache = IsolatedCacheRoot.Create();
        var workspacePath = WorkspaceTestDirectoryFactory.Create();
        var killedInstanceId = Guid.NewGuid();
        var survivorInstanceId = Guid.NewGuid();
        var workspaceHash = WorkspaceHash.Compute(workspacePath).Value;
        var killedSubtreePath = Path.Combine(cache.Path, workspaceHash, killedInstanceId.ToString("D"));
        var survivorSubtreePath = Path.Combine(cache.Path, workspaceHash, survivorInstanceId.ToString("D"));
        string[] sharedArgs = ["--retention", "1s", "--cache-root", cache.Path];

        await using var killed = await EngineTestProcess.StartAsync(
            workspacePath,
            killedInstanceId,
            ct,
            extraArguments: sharedArgs);
        await using var survivor = await EngineTestProcess.StartAsync(
            workspacePath,
            survivorInstanceId,
            ct,
            extraArguments: sharedArgs);

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

        await WaitForSubtreeAsync(killedSubtreePath);
        await WaitForSubtreeAsync(survivorSubtreePath);
        var killedSubtreeExistedBeforeKill = Directory.Exists(killedSubtreePath);
        var survivorSubtreeExistedBeforeKill = Directory.Exists(survivorSubtreePath);

        // Act
        killed.Process.Kill(entireProcessTree: true);
        await killed.Process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

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

        try
        {
            await EngineWireTestClient.ShutdownGracefullyAsync(survivor, workspacePath, survivorInstanceId, ct);
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
    }
}
