namespace AutoContext.Client.Core.Tests.Support.Engine.Rpc;

using AutoContext.Client.Core;
using AutoContext.Client.Core.Engine;
using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Shared arrange helpers for the resolver tests: a fresh absolute
/// workspace path, the endpoint address the resolver derives for it, a
/// fast connect budget that keeps failure paths from waiting out the
/// production ten-second window, and an <see cref="EngineConnector"/>
/// wired against supplied fakes.
/// </summary>
internal static class ConnectorTestHarness
{
    public static EngineConnectBudget FastBudget { get; } = new()
    {
        WarmConnectTimeout = TimeSpan.FromMilliseconds(250),
        ColdConnectBudget = TimeSpan.FromSeconds(5),
        ColdConnectAttemptTimeout = TimeSpan.FromMilliseconds(500),
        InitialRetryDelay = TimeSpan.FromMilliseconds(20),
        MaxRetryDelay = TimeSpan.FromMilliseconds(100),
    };

    public static string NewWorkspacePath()
        => Path.Combine(Path.GetTempPath(), "ac-client-tests", Guid.NewGuid().ToString("N"));

    public static string Address(EndpointKind kind, string workspacePath, Guid instanceId)
        => new Endpoint(kind, WorkspaceHash.Compute(workspacePath).Value, instanceId).ToString();

    public static EngineConnector CreateConnector(
        ClientOptions options, IEngineSpawner spawner, EngineConnectBudget? budget = null)
        => new(
            Options.Create(options),
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            spawner,
            budget ?? FastBudget,
            NullLogger<EngineConnector>.Instance);
}
