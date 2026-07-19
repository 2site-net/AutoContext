namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;

/// <summary>
/// No-op <see cref="IConfigReloader"/> test double that counts reloads,
/// letting <c>McpSdkAdapter</c> tests drive the request path without a
/// stateful <see cref="ConfigFileManager"/> or disk I/O.
/// </summary>
internal sealed class FakeConfigReloader : IConfigReloader
{
    public int ReloadCallCount { get; private set; }

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        ReloadCallCount++;
        return Task.CompletedTask;
    }
}
