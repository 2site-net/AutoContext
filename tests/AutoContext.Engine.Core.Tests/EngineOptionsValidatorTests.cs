namespace AutoContext.Engine.Core.Tests;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Tests.Support.Options;

using Microsoft.Extensions.Options;

public sealed class EngineOptionsValidatorTests
{
    [Fact]
    public void Should_accept_minimal_valid_options()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, ValidateOptionsResultTestFormatter.ReportFailures(result));
    }

    [Fact]
    public void Should_reject_missing_workspace_path()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.WorkspacePath = string.Empty;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("WorkspacePath", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_relative_workspace_path()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.WorkspacePath = "relative/path";

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("absolute path", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_empty_instance_id()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.InstanceId = Guid.Empty;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("InstanceId", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_accept_empty_instance_label()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.InstanceLabel = string.Empty;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, ValidateOptionsResultTestFormatter.ReportFailures(result));
    }

    [Fact]
    public void Should_reject_instance_label_exceeding_max_length()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.InstanceLabel = new string('a', EngineOptions.InstanceLabelMaxLength + 1);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("InstanceLabel", StringComparison.Ordinal));
            });
    }

    [Theory]
    [InlineData("line\nbreak")]
    [InlineData("tab\there")]
    [InlineData("control\u0001char")]
    [InlineData("non-ascii: é")]
    public void Should_reject_instance_label_with_non_printable_ascii(string label)
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.InstanceLabel = label;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("InstanceLabel", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_accept_idle_timeout_zero_as_disabled_sentinel()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.IdleTimeout = TimeSpan.Zero;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, ValidateOptionsResultTestFormatter.ReportFailures(result));
    }

    [Fact]
    public void Should_reject_negative_idle_timeout()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.IdleTimeout = TimeSpan.FromSeconds(-1);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("IdleTimeout", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_non_positive_parent_process_id()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.ParentProcessId = 0;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("ParentProcessId", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_accept_unset_parent_process_id()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.ParentProcessId = null;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, ValidateOptionsResultTestFormatter.ReportFailures(result));
    }

    [Fact]
    public void Should_reject_negative_retention()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.Retention = TimeSpan.FromSeconds(-1);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("Retention", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_undefined_log_rotation_size()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.LogRotation = (LogRotationSize)99;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("LogRotation", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_undefined_mcp_server_mode()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.McpServerMode = (EngineMcpServerMode)99;

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("McpServerMode", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_relative_corpus_root_override()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.CorpusRootOverride = "relative/corpus";

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("CorpusRootOverride", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_relative_cache_root_override()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.CacheRootOverride = "relative/cache";

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("CacheRootOverride", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_reject_relative_resources_root_override()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = EngineOptionsFakeData.CreateValidOptions();
        options.ResourcesRootOverride = "relative/resources";

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.Contains(result.Failures, m => m.Contains("ResourcesRootOverride", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Should_report_every_violation_in_a_single_pass()
    {
        // Arrange
        var validator = new EngineOptionsValidator();
        var options = new EngineOptions
        {
            WorkspacePath = string.Empty,
            InstanceId = Guid.Empty,
            IdleTimeout = TimeSpan.FromSeconds(-1),
            Retention = TimeSpan.FromSeconds(-1),
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.Failed),
            () =>
            {
                Assert.NotNull(result.Failures);
                Assert.True(result.Failures.Count() >= 4, ValidateOptionsResultTestFormatter.ReportFailures(result));
            });
    }

    [Fact]
    public void Should_throw_on_null_options()
    {
        // Arrange
        var validator = new EngineOptionsValidator();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));
    }
}
