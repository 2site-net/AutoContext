namespace AutoContext.Engine.Core.Tests.Lifecycle;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class PerWorkspaceInstanceGuardTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PerWorkspaceInstanceGuard(
                null!,
                PerWorkspaceInstanceGuardFixture.CreateTransport(),
                NullLogger<PerWorkspaceInstanceGuard>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_transport()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PerWorkspaceInstanceGuard(
                Options.Create(PerWorkspaceInstanceGuardFixture.CreateOptions()),
                null!,
                NullLogger<PerWorkspaceInstanceGuard>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PerWorkspaceInstanceGuard(
                Options.Create(PerWorkspaceInstanceGuardFixture.CreateOptions()),
                PerWorkspaceInstanceGuardFixture.CreateTransport(),
                null!));
    }

    [Fact]
    public async Task Should_complete_silently_when_no_peer_is_bound()
    {
        var options = PerWorkspaceInstanceGuardFixture.CreateOptions();
        var guard = PerWorkspaceInstanceGuardFixture.CreateGuard(options);

        await guard.EnsureUniqueAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_throw_IOException_when_live_peer_holds_the_rpc_endpoint()
    {
        var options = PerWorkspaceInstanceGuardFixture.CreateOptions();
        var pipeName = PerWorkspaceInstanceGuardFixture.ComputeRpcPipeName(options);
        await using var peer = PerWorkspaceInstanceGuardFixture.CreatePeerListener(options);
        var acceptTask = peer.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        var guard = PerWorkspaceInstanceGuardFixture.CreateGuard(options);

        var ex = await Assert.ThrowsAsync<IOException>(() =>
            guard.EnsureUniqueAsync(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Contains(pipeName, ex.Message, StringComparison.Ordinal),
            () => Assert.Contains(options.InstanceId.ToString("D"), ex.Message, StringComparison.OrdinalIgnoreCase));

        await DrainAsync(acceptTask);

        static async Task DrainAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task Should_not_throw_when_peer_is_in_different_workspace_with_same_instance_id()
    {
        var sharedInstanceId = Guid.NewGuid();
        var peerOptions = new EngineOptions
        {
            WorkspacePath = PerWorkspaceInstanceGuardFixture.CreateOptions().WorkspacePath + "-other",
            InstanceId = sharedInstanceId,
        };
        await using var peer = PerWorkspaceInstanceGuardFixture.CreatePeerListener(peerOptions);
        var ourOptions = new EngineOptions
        {
            WorkspacePath = PerWorkspaceInstanceGuardFixture.CreateOptions().WorkspacePath,
            InstanceId = sharedInstanceId,
        };
        var guard = PerWorkspaceInstanceGuardFixture.CreateGuard(ourOptions);

        await guard.EnsureUniqueAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_not_throw_when_peer_is_in_same_workspace_with_different_instance_id()
    {
        var sharedWorkspace = PerWorkspaceInstanceGuardFixture.CreateOptions().WorkspacePath;
        var peerOptions = new EngineOptions
        {
            WorkspacePath = sharedWorkspace,
            InstanceId = Guid.NewGuid(),
        };
        await using var peer = PerWorkspaceInstanceGuardFixture.CreatePeerListener(peerOptions);
        var ourOptions = new EngineOptions
        {
            WorkspacePath = sharedWorkspace,
            InstanceId = Guid.NewGuid(),
        };
        var guard = PerWorkspaceInstanceGuardFixture.CreateGuard(ourOptions);

        await guard.EnsureUniqueAsync(TestContext.Current.CancellationToken);
    }
}
