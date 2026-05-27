namespace AutoContext.Engine.Tests.Integration;

using System.Globalization;

using AutoContext.Engine.Tests.Support.Integration;

using Xunit.Sdk;

/// <summary>
/// Integration coverage for the <c>autocontext-engine</c> binary in
/// daemon role — spawns the published executable, dials each of the
/// four pipes to confirm the atomic bind, completes the
/// <c>Engine.Hello</c> handshake on <c>rpc</c>, and confirms that
/// <c>Engine.Shutdown</c> drains the host cleanly and produces a
/// zero exit code.
/// </summary>
/// <remarks>
/// Phase 1 (row 15) of the engine implementation plan stands this
/// harness up; subsequent phases extend it with workers and
/// per-pipe payload assertions. Gated with the repository's
/// <c>Category=Smoke</c> trait so it runs under
/// <c>.\build.ps1 Compile -Smoke DotNet</c> and stays out of the
/// default unit-test pass.
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class EngineSpawnTests
{
    [Fact]
    public async Task Should_bind_each_pipe_complete_handshake_and_exit_zero_on_shutdown()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var workspacePath = WorkspaceTestDirectoryFactory.Create();
        var instanceId = Guid.NewGuid();

        await using var engine = await EngineTestProcess.StartAsync(
            workspacePath, instanceId, ct);

        try
        {
            await EngineSpawnTestScenario.RunAsync(engine, workspacePath, instanceId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var exitCode = engine.Process.HasExited
                ? engine.Process.ExitCode.ToString(CultureInfo.InvariantCulture)
                : "<running>";
            var stderr = string.Join(Environment.NewLine, engine.StandardErrorLines);

            throw new XunitException(
                $"Scenario failed. Engine HasExited={engine.Process.HasExited}, ExitCode={exitCode}."
                + $"{Environment.NewLine}Stderr:{Environment.NewLine}{(stderr.Length == 0 ? "(no stderr)" : stderr)}",
                ex);
        }
    }
}
