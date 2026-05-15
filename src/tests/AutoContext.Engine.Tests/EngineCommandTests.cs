namespace AutoContext.Engine.Tests;

using System.CommandLine;
using System.CommandLine.Parsing;

using AutoContext.Engine;
using AutoContext.Engine.Core;
using AutoContext.Engine.Tests.Testing.Utils;

/// <summary>
/// Direct tests for <see cref="EngineCommand"/> — drives the
/// <c>System.CommandLine</c> parser and the
/// <see cref="EngineCommand.TryBuildOptions"/> cross-option rules
/// without going through <c>Program.Main</c>. These cover argv
/// shape, per-option validators, closed value sets, and the
/// daemon/MCP role split.
/// </summary>
public sealed class EngineCommandTests
{
    [Fact]
    public void Should_fail_on_empty_argv_with_workspace_required_error()
    {
        // Arrange
        var command = new EngineCommand();

        // Act
        var parseResult = command.Parse([]);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--workspace", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_fail_on_unknown_switch()
    {
        // Arrange
        var command = new EngineCommand();

        // Act
        var parseResult = command.Parse(["--bogus", "value"]);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--bogus", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_parse_minimum_daemon_argv()
    {
        // Arrange
        var command = new EngineCommand();

        // Act
        var parseResult = command.Parse(EngineCommandArgsFakeData.CreateValidDaemonArgs());

        // Assert
        Assert.Empty(parseResult.Errors);
        var built = command.TryBuildOptions(parseResult, out var options, out var error);
        Assert.Multiple(
            () => Assert.True(built, error),
            () => Assert.Null(error),
            () => Assert.Equal(EngineCommandArgsFakeData.GetWorkspacePathArgValue(), options.WorkspacePath),
            () => Assert.Equal(Guid.Parse(EngineCommandArgsFakeData.GetInstanceIdArgValue()), options.InstanceId),
            () => Assert.Equal(EngineMcpServerMode.Off, options.McpServerMode));
    }

    [Fact]
    public void Should_parse_all_daemon_role_switches()
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", EngineCommandArgsFakeData.GetInstanceIdArgValue(),
            "--instance-label", "vscode (v0.9.5); engine (v0.9.5)",
            "--idle-timeout", "0",
            "--parent-pid", "1234",
            "--retention", "12h",
            "--logging", "debug",
        };

        // Act
        var parseResult = command.Parse(args);
        var built = command.TryBuildOptions(parseResult, out var options, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(parseResult.Errors),
            () => Assert.True(built, error),
            () => Assert.Equal("vscode (v0.9.5); engine (v0.9.5)", options.InstanceLabel),
            () => Assert.Equal(TimeSpan.Zero, options.IdleTimeout),
            () => Assert.Equal(1234, options.ParentProcessId),
            () => Assert.Equal(TimeSpan.FromHours(12), options.Retention),
            () => Assert.Equal(EngineLoggingVerbosity.Debug, options.Logging));
    }

    [Fact]
    public void Should_fail_role_check_when_daemon_role_is_missing_instance_id()
    {
        // Arrange
        var command = new EngineCommand();
        var parseResult = command.Parse(["--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue()]);

        // Act
        var built = command.TryBuildOptions(parseResult, out _, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(parseResult.Errors),
            () => Assert.False(built),
            () => Assert.NotNull(error),
            () => Assert.Contains("--instance-id", error!, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_reject_uppercase_hex_in_instance_id()
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", "11111111-2222-4333-8444-AAAAAAAAAAAA",
        };

        // Act
        var parseResult = command.Parse(args);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--instance-id", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_reject_malformed_instance_id()
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", "not-a-uuid",
        };

        // Act
        var parseResult = command.Parse(args);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--instance-id", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_reject_negative_idle_timeout()
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", EngineCommandArgsFakeData.GetInstanceIdArgValue(),
            "--idle-timeout", "-1",
        };

        // Act
        var parseResult = command.Parse(args);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--idle-timeout", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_reject_non_positive_parent_pid()
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", EngineCommandArgsFakeData.GetInstanceIdArgValue(),
            "--parent-pid", "0",
        };

        // Act
        var parseResult = command.Parse(args);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--parent-pid", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0", 0, 0, 0, 0)]
    [InlineData("30s", 0, 0, 0, 30)]
    [InlineData("15m", 0, 0, 15, 0)]
    [InlineData("12h", 0, 12, 0, 0)]
    [InlineData("7d", 7, 0, 0, 0)]
    public void Should_parse_retention_duration(
        string value,
        int days,
        int hours,
        int minutes,
        int seconds)
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", EngineCommandArgsFakeData.GetInstanceIdArgValue(),
            "--retention", value,
        };

        // Act
        var parseResult = command.Parse(args);
        var built = command.TryBuildOptions(parseResult, out var options, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(parseResult.Errors),
            () => Assert.True(built, error),
            () => Assert.Equal(new TimeSpan(days, hours, minutes, seconds), options.Retention));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("5x")]
    [InlineData("s")]
    [InlineData("-1d")]
    public void Should_reject_malformed_retention(string value)
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", EngineCommandArgsFakeData.GetInstanceIdArgValue(),
            "--retention", value,
        };

        // Act
        var parseResult = command.Parse(args);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--retention", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("normal", EngineLoggingVerbosity.Normal)]
    [InlineData("debug", EngineLoggingVerbosity.Debug)]
    public void Should_parse_logging_verbosity(string value, EngineLoggingVerbosity expected)
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", EngineCommandArgsFakeData.GetInstanceIdArgValue(),
            "--logging", value,
        };

        // Act
        var parseResult = command.Parse(args);
        var built = command.TryBuildOptions(parseResult, out var options, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(parseResult.Errors),
            () => Assert.True(built, error),
            () => Assert.Equal(expected, options.Logging));
    }

    [Fact]
    public void Should_reject_unknown_logging_value()
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--instance-id", EngineCommandArgsFakeData.GetInstanceIdArgValue(),
            "--logging", "verbose",
        };

        // Act
        var parseResult = command.Parse(args);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("verbose", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_parse_minimum_mcp_server_argv()
    {
        // Arrange
        var command = new EngineCommand();

        // Act
        var parseResult = command.Parse(EngineCommandArgsFakeData.CreateValidMcpServerArgs());
        var built = command.TryBuildOptions(parseResult, out var options, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(parseResult.Errors),
            () => Assert.True(built, error),
            () => Assert.Equal(EngineMcpServerMode.WithStdio, options.McpServerMode),
            () => Assert.Equal(EngineCommandArgsFakeData.GetWorkspacePathArgValue(), options.WorkspacePath));
    }

    [Fact]
    public void Should_reject_unknown_mcp_server_value()
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--mcp-server", "with-http",
        };

        // Act
        var parseResult = command.Parse(args);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("with-http", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("--instance-id", "11111111-2222-4333-8444-555555555555")]
    [InlineData("--instance-label", "vscode")]
    [InlineData("--idle-timeout", "0")]
    [InlineData("--parent-pid", "1234")]
    [InlineData("--retention", "1d")]
    [InlineData("--logging", "debug")]
    public void Should_reject_daemon_only_switches_in_mcp_server_role(
        string switchName,
        string value)
    {
        // Arrange
        var command = new EngineCommand();
        var args = new[]
        {
            "--workspace", EngineCommandArgsFakeData.GetWorkspacePathArgValue(),
            "--mcp-server", "with-stdio",
            switchName, value,
        };

        // Act
        var parseResult = command.Parse(args);
        var built = command.TryBuildOptions(parseResult, out _, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(parseResult.Errors),
            () => Assert.False(built),
            () => Assert.NotNull(error),
            () => Assert.Contains(switchName, error!, StringComparison.Ordinal),
            () => Assert.Contains("--mcp-server with-stdio", error!, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_fail_when_mcp_role_is_missing_workspace()
    {
        // Arrange
        var command = new EngineCommand();

        // Act
        var parseResult = command.Parse(["--mcp-server", "with-stdio"]);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(
            parseResult.Errors,
            e => e.Message.Contains("--workspace", StringComparison.Ordinal));
    }
}
