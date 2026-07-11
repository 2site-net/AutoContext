namespace AutoContext.Framework.Pipes.Tests;

using AutoContext.Framework.Pipes;
using AutoContext.Framework.Pipes.Tests.Support;
using AutoContext.Framework.Tests.Support.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

[Trait("Category", "Integration")]
public sealed class PipeListenerBacklogTests
{
    [Fact]
    public async Task Should_accept_a_client_that_connected_before_RunAsync_starts()
    {
        // Arrange
        var name = PipeTestServer.UniqueName("actx-plb-test");
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        await using var bound = listener.Bind();
        await using var client = PipeListenerTestHarness.CreateClient(name);

        // Act
        await client.ConnectAsync(
            PipeListenerTestHarness.DefaultConnectTimeoutMs, TestContext.Current.CancellationToken);
        var accepted = await PipeListenerTestHarness.WasFirstConnectionAcceptedAsync(
            bound, within: TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted, "A client that connected before RunAsync started was never accepted.");
    }

    [Fact]
    public async Task Should_not_fault_the_accept_loop_on_a_rapid_connect_disconnect_burst()
    {
        // Arrange
        var name = PipeTestServer.UniqueName("actx-plb-test");
        var listener = new PipeListener(name, NullLogger<PipeListener>.Instance);
        await using var bound = listener.Bind();

        // Act
        var loopFault = await PipeListenerTestHarness.CaptureAcceptLoopFaultDuringBurstAsync(
            bound, name, cycles: 100, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(loopFault);
    }
}
