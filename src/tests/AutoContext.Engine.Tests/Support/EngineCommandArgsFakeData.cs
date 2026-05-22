namespace AutoContext.Engine.Tests.Support;

/// <summary>
/// Centralised fake-data factory for <see cref="EngineCommand"/>
/// argument-vector tests. Keeps the individual <c>[Fact]</c>/<c>[Theory]</c>
/// bodies focused on the behaviour under test rather than the boilerplate
/// of building valid argv vectors.
/// </summary>
internal static class EngineCommandArgsFakeData
{
    private const string InstanceId = "11111111-2222-4333-8444-555555555555";

    public static string GetInstanceIdArgValue()
        => InstanceId;

    public static string GetWorkspacePathArgValue()
        => OperatingSystem.IsWindows() ? @"C:\repo\sample" : "/repo/sample";

    public static string[] CreateValidDaemonArgs() =>
    [
        "--workspace", GetWorkspacePathArgValue(),
        "--instance-id", InstanceId,
    ];

    public static string[] CreateValidMcpServerArgs() =>
    [
        "--workspace", GetWorkspacePathArgValue(),
        "--mcp-server", "with-stdio",
    ];
}
