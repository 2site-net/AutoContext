namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Engine.Protocol.Messages.Workspace;

public sealed class WorkspaceRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new WorkspaceRpcClient(connection: null!));

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_report_the_engine_identity_from_an_in_process_engine()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var engine = await InProcessEngineTestHarness.StartAsync(cancellationToken);
        await using var client = await engine.ConnectAsync(cancellationToken);

        // Act
        var detected = await client.Workspace.DetectAsync(cancellationToken);
        var info = await client.Workspace.InfoAsync(cancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(engine.InstanceId, info.InstanceId),
            () => Assert.NotEmpty(info.EngineVersion),
            () => Assert.NotNull(detected.Flags));
    }

    [Fact]
    public async Task Should_send_the_detect_method()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new WorkspaceRpcClient(pair.ClientConnection);

        // Act
        var call = client.DetectAsync(cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(WorkspaceMethods.Detect, request.Method),
            () => Assert.NotNull(result));
    }

    [Fact]
    public async Task Should_send_the_info_method()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new WorkspaceRpcClient(pair.ClientConnection);

        // Act
        var call = client.InfoAsync(cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Equal(WorkspaceMethods.Info, request.Method);
    }
}
