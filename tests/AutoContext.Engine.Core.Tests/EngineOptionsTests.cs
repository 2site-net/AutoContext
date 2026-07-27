namespace AutoContext.Engine.Core.Tests;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;

public sealed class EngineOptionsTests
{
    [Fact]
    public void Should_default_to_documented_daemon_role_values()
    {
        // Arrange + Act
        var options = new EngineOptions();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(string.Empty, options.WorkspacePath),
            () => Assert.Equal(Guid.Empty, options.InstanceId),
            () => Assert.Equal(string.Empty, options.InstanceLabel),
            () => Assert.Equal(TimeSpan.FromSeconds(300), options.IdleTimeout),
            () => Assert.Null(options.ParentProcessId),
            () => Assert.Equal(TimeSpan.FromDays(1), options.Retention),
            () => Assert.Equal(LogRotationSize.Small, options.LogRotation),
            () => Assert.Equal(EngineMcpServerMode.Off, options.McpServerMode),
            () => Assert.Null(options.CorpusRootOverride));
    }

    [Fact]
    public void Should_expose_default_idle_timeout_as_documented_constant()
    {
        // Act + Assert
        Assert.Equal(TimeSpan.FromSeconds(300), EngineOptions.DefaultIdleTimeout);
    }

    [Fact]
    public void Should_expose_default_retention_as_documented_constant()
    {
        // Act + Assert
        Assert.Equal(TimeSpan.FromDays(1), EngineOptions.DefaultRetention);
    }

    [Fact]
    public void Should_expose_instance_label_max_length_as_documented_constant()
    {
        // Act + Assert
        Assert.Equal(200, EngineOptions.InstanceLabelMaxLength);
    }
}
